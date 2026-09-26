using System.Text;
using System.Text.Json.Serialization;
using EventHubAPI.Auth;
using ImageStorage;
using EventImport;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.Extensions.Options;
using Minio;
using Services;
using MaxBotCore.Extensions;
using Scheduler;
using Services.Interfaces;
using Storage;

if (args.Contains("--hash-admin-password"))
{
	var password = new StringBuilder();
	if (Console.IsInputRedirected)
	{
		password.Append(Console.ReadLine());
	}
	else
	{
		Console.Write("Пароль админа: ");
		while (true)
		{
			var key = Console.ReadKey(intercept: true);
			if (key.Key == ConsoleKey.Enter)
			{
				break;
			}
			if (key.Key == ConsoleKey.Backspace && password.Length > 0)
			{
				password.Length--;
			}
			else if (!char.IsControl(key.KeyChar))
			{
				password.Append(key.KeyChar);
			}
		}
		Console.WriteLine();
	}
	if (password.Length == 0)
	{
		throw new ArgumentException("Пароль не должен быть пустым.");
	}
	Console.WriteLine(new PasswordHasher<string>().HashPassword("admin", password.ToString()));
	return;
}

var envPath = Path.Combine(Directory.GetCurrentDirectory(), ".env");
if (!File.Exists(envPath))
{
	envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
}
if (File.Exists(envPath))
{
	DotNetEnv.Env.Load(envPath);
}

var builder = WebApplication.CreateBuilder(args);
var signingKey = builder.Configuration["Jwt:SigningKey"]
	?? throw new InvalidOperationException("Jwt:SigningKey не задан.");
if (Encoding.UTF8.GetByteCount(signingKey) < 32)
{
	throw new InvalidOperationException("Jwt:SigningKey должен содержать не менее 32 байт.");
}

builder.Services.AddOpenApi(options =>
{
	options.AddOperationTransformer((operation, context, _) =>
	{
		if (context.Description.HttpMethod == "POST" &&
			context.Description.RelativePath?.Trim('/') == "api/admin/images")
		{
			operation.RequestBody = new OpenApiRequestBody
			{
				Required = true,
				Content = new Dictionary<string, OpenApiMediaType>
				{
					["multipart/form-data"] = new()
					{
						Schema = new OpenApiSchema
						{
							Type = JsonSchemaType.Object,
							Properties = new Dictionary<string, IOpenApiSchema>
							{
								["file"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "binary" }
							},
							Required = new HashSet<string> { "file" }
						}
					}
				}
			};
		}
		var metadata = context.Description.ActionDescriptor.EndpointMetadata;
		if (metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any())
		{
			return Task.CompletedTask;
		}
		operation.Security = [];
		return Task.CompletedTask;
	});
	options.AddDocumentTransformer((document, _, _) =>
	{
		document.Components ??= new OpenApiComponents();
		document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
		document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
		{
			Type = SecuritySchemeType.Http,
			Scheme = "bearer",
			BearerFormat = "JWT"
		};
		document.Security =
		[
			new OpenApiSecurityRequirement
			{
				[new OpenApiSecuritySchemeReference("Bearer", document)] = []
			}
		];
		return Task.CompletedTask;
	});
});
builder.Services.AddControllers().AddJsonOptions(options =>
	options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
	builder.Configuration.GetConnectionString("Default") ?? AppDbContextFactory.BuildPostgresConnectionString()));
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IUserEventService, UserEventService>();
builder.Services.AddScoped<IRecomendationService, RecomendationService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddEventImport();
builder.Services.AddMaxBotCore(builder.Configuration);
builder.Services.AddBotScheduler();
builder.Services.Configure<MinioOptions>(builder.Configuration.GetSection("Minio"));
builder.Services.AddSingleton<IMinioClient>(services =>
{
	var options = services.GetRequiredService<IOptions<MinioOptions>>().Value;
	var client = new MinioClient()
		.WithEndpoint(options.Endpoint)
		.WithCredentials(options.AccessKey, options.SecretKey);
	if (options.UseSsl)
	{
		client = client.WithSSL();
	}
	return client.Build();
});
builder.Services.AddSingleton<IImageStorageService, MinioImageStorageService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<MaxInitDataValidator>();
builder.Services.AddSingleton<AdminCredentialVerifier>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
	.AddJwtBearer(options =>
	{
		options.MapInboundClaims = false;
		options.TokenValidationParameters = new TokenValidationParameters
		{
			ValidateIssuer = true,
			ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "EventHubAPI",
			ValidateAudience = true,
			ValidAudience = builder.Configuration["Jwt:Audience"] ?? "EventHubClients",
			ValidateIssuerSigningKey = true,
			IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
			ValidateLifetime = true,
			ClockSkew = TimeSpan.FromMinutes(1),
			RoleClaimType = "role"
		};
	});
builder.Services.AddAuthorization();

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
if (origins.Length > 0)
{
	builder.Services.AddCors(options => options.AddPolicy("MiniApp", policy =>
		policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));
}

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
	var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
	await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
	var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
	var status = error switch
	{
		ArgumentException => StatusCodes.Status400BadRequest,
		KeyNotFoundException => StatusCodes.Status404NotFound,
		InvalidOperationException => StatusCodes.Status409Conflict,
		_ => StatusCodes.Status500InternalServerError
	};
	if (status == StatusCodes.Status500InternalServerError)
	{
		context.RequestServices.GetRequiredService<ILoggerFactory>()
			.CreateLogger("Api").LogError(error, "Ошибка обработки запроса");
	}
	await Results.Problem(statusCode: status,
		detail: status == StatusCodes.Status500InternalServerError ? "Внутренняя ошибка." : error?.Message)
		.ExecuteAsync(context);
}));

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
	app.MapOpenApi();
	app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "EventHub API"));
}

app.UseHttpsRedirection();
if (origins.Length > 0)
{
	app.UseCors("MiniApp");
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
