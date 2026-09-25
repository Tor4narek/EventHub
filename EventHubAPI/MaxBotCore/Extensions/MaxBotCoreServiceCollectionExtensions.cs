using MaxBotCore.Client;
using MaxBotCore.Configuration;
using MaxBotCore.Contracts.Updates;
using MaxBotCore.Controllers;
using MaxBotCore.Handlers;
using MaxBotCore.Routing;
using MaxBotCore.Scenarios;
using MaxBotCore.Work;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Services;
using Services.Interfaces;

namespace MaxBotCore.Extensions;

/// <summary>
/// Единая точка регистрации всех компонентов MaxBotCore в хост-приложении.
/// </summary>
public static class MaxBotCoreServiceCollectionExtensions
{
	public static IServiceCollection AddMaxBotCore(
		this IServiceCollection services,
		IConfiguration configuration)
	{
		// 1. Настройки.
		services.AddOptions<MaxOptions>()
			.Bind(configuration.GetSection(MaxOptions.SectionName))
			.ValidateDataAnnotations()
			.ValidateOnStart();

		// 2. HTTP-клиент MAX API.
		services.AddHttpClient<IMaxBotClient, MaxBotClient>((sp, client) =>
		{
			var options = sp.GetRequiredService<IOptions<MaxOptions>>().Value;
			client.BaseAddress = new Uri(options.ApiBaseUrl.TrimEnd('/') + "/");
			client.Timeout = TimeSpan.FromSeconds(options.HttpTimeoutSeconds);
			client.DefaultRequestHeaders.Remove("Authorization");
			client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", options.BotToken);
		});

		// 3. Маршрутизация Update.
		services.AddScoped<IMaxUpdateRouter, MaxUpdateRouter>();
		services.AddScoped<IBotScenario, BotScenario>();
		services.AddScoped<IBotUpdateInboxService, BotUpdateInboxService>();
		services.AddHostedService<BotUpdateWorker>();

		// 4. Обработчики событий MAX.
		services.AddScoped<IMaxUpdateHandler<MessageCreatedUpdate>, MessageCreatedHandler>();
		services.AddScoped<IMaxUpdateHandler<MessageCallbackUpdate>, MessageCallbackHandler>();
		services.AddScoped<IMaxUpdateHandler<BotStartedUpdate>, BotStartedHandler>();
		services.AddScoped<IMaxUpdateHandler<BotStoppedUpdate>, BotStoppedHandler>();
		services.AddScoped<IMaxUpdateHandler<BotAddedUpdate>, BotAddedHandler>();
		services.AddScoped<IMaxUpdateHandler<BotRemovedUpdate>, BotRemovedHandler>();

		// 5. Фильтр валидации секрета для webhook.
		services.AddScoped<MaxWebhookSecretFilter>();

		return services;
	}
}
