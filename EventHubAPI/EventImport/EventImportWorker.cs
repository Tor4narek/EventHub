using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace EventImport;

public class EventImportWorker(IServiceScopeFactory scopes, ILogger<EventImportWorker> logger) : BackgroundService
{
	protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.WhenAll(ConsumeAsync(stoppingToken), ConsumeAsync(stoppingToken));

	private async Task ConsumeAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await using var scope = scopes.CreateAsyncScope();
				var service = scope.ServiceProvider.GetRequiredService<EventImportService>();
				if (!await service.ProcessNextAsync(stoppingToken)) await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
			catch (Exception error)
			{
				logger.LogError(error, "Ошибка обработки очереди импорта; незавершённая запись будет повторена после истечения блокировки");
				await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
			}
		}
	}
}

public static class EventImportExtensions
{
	public static IServiceCollection AddEventImport(this IServiceCollection services)
	{
		services.AddHttpClient<IEventSourceParser, ItmoEventParser>(client =>
		{
			client.DefaultRequestHeaders.UserAgent.ParseAdd("EventHub/1.0 (+event-import)");
			client.Timeout = TimeSpan.FromSeconds(35);
		}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
		services.AddScoped<EventImportService>();
		services.AddScoped<IEventImportService>(provider => provider.GetRequiredService<EventImportService>());
		services.AddHostedService<EventImportWorker>();
		return services;
	}
}
