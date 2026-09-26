using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using EventHubAPI.Controllers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Services;
using Services.Interfaces;
using Xunit;

namespace EventImport.Tests;

public class ImportApiTests(ImportDatabase database) : IClassFixture<ImportDatabase>
{
	private sealed class FixtureParser : IEventSourceParser
	{
		public Uri NormalizeUrl(string url) => new ItmoEventParser(new HttpClient()).NormalizeUrl(url);
		public Task<ParsedEvent> ParseAsync(Uri url, CancellationToken cancellationToken) => Task.FromResult(ItmoEventParser.ParseHtml(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "itmo-122043.html"))));
	}

	[PostgresFact]
	public async Task AdminCanImportReviewAndConfirmViaHttpWithoutPublishing()
	{
		var builder = WebApplication.CreateBuilder();
		builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders(); builder.Logging.AddConsole();
		builder.Services.AddControllers().AddApplicationPart(typeof(AdminImportsController).Assembly)
			.AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
		builder.Services.AddScoped(_ => database.CreateContext());
		builder.Services.AddSingleton<IEventSourceParser, FixtureParser>();
		builder.Services.AddScoped<IEventService, EventService>();
		builder.Services.AddScoped<EventImportService>();
		builder.Services.AddScoped<IEventImportService>(p => p.GetRequiredService<EventImportService>());
		builder.Services.AddHostedService<EventImportWorker>();
		var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(new string('x', 40)));
		builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
		{
			options.MapInboundClaims = false;
			options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = false, ValidateAudience = false, IssuerSigningKey = key, RoleClaimType = "role" };
		});
		builder.Services.AddAuthorization();
		await using var app = builder.Build(); app.UseAuthentication(); app.UseAuthorization(); app.MapControllers();
		await app.StartAsync();
		try
		{
			var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
			using var client = new HttpClient { BaseAddress = new Uri(address) };
			Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/imports")).StatusCode);
			var jwt = new JwtSecurityToken(claims: [new Claim("role", "Admin")], expires: DateTime.UtcNow.AddMinutes(5), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
			client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
			var start = await client.PostAsJsonAsync("/api/admin/imports", new { urls = new[] { "https://itmo.events/events/904", "https://itmo.events/events/904/?utm_source=test" } });
			Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
			var runId = (await start.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("importId").GetGuid();
			JsonElement item = default;
			for (var attempt = 0; attempt < 40; attempt++)
			{
				var run = await client.GetFromJsonAsync<JsonElement>($"/api/admin/imports/{runId}");
				Assert.Equal(1, run.GetProperty("items").GetArrayLength());
				item = run.GetProperty("items")[0];
				if (item.GetProperty("status").GetString() == "Ready") break;
				await Task.Delay(250);
			}
			Assert.Equal("Ready", item.GetProperty("status").GetString());
			Assert.Equal("ул.Ломоносова, д.9", item.GetProperty("location").GetString());
			Guid tagId;
			await using (var db = database.CreateContext())
			{
				var tag = new Storage.Entities.Tag { Id = Guid.NewGuid(), Name = "API test", Description = "Science", Examples = [] }; db.Tags.Add(tag); await db.SaveChangesAsync(); tagId = tag.Id;
			}
			var itemId = item.GetProperty("id").GetGuid();
			var path = $"/api/admin/imports/{runId}/items/{itemId}";
			var update = await client.PutAsJsonAsync(path, new { title = "Reviewed event", description = "Reviewed description", location = "Online", eventDateTime = DateTime.UtcNow.AddDays(3), deadline = (DateTime?)null, tagIds = new[] { tagId }, mainImg = (string?)null });
			Assert.Equal(HttpStatusCode.OK, update.StatusCode);
			var responses = await Task.WhenAll(client.PostAsync(path + "/confirm", null), client.PostAsync(path + "/confirm", null));
			foreach (var response in responses) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
			var eventId = (await responses[0].Content.ReadFromJsonAsync<JsonElement>()).GetProperty("eventId").GetGuid();
			Assert.Equal(eventId, (await responses[1].Content.ReadFromJsonAsync<JsonElement>()).GetProperty("eventId").GetGuid());
			var created = await client.GetFromJsonAsync<JsonElement>($"/api/admin/events/{eventId}");
			Assert.Equal("Draft", created.GetProperty("eventStatus").GetString()); Assert.True(created.GetProperty("tagsConfirmed").GetBoolean());
		}
		finally { await app.StopAsync(); }
	}
}
