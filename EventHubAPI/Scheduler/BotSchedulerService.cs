using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Scheduler;

public sealed class BotSchedulerService : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<BotSchedulerService> _logger;

	public BotSchedulerService(IServiceScopeFactory scopeFactory, ILogger<BotSchedulerService> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
		var lastDigestHour = DateTime.MinValue;
		do
		{
			try
			{
				await using var scope = _scopeFactory.CreateAsyncScope();
				var now = DateTime.UtcNow;
				await scope.ServiceProvider.GetRequiredService<ReminderJob>().RunAsync(now, stoppingToken);
				var digestHour = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc);
				if (digestHour != lastDigestHour)
				{
					await scope.ServiceProvider.GetRequiredService<WeeklyDigestJob>().RunAsync(now, stoppingToken);
					lastDigestHour = digestHour;
				}
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Ошибка фоновой задачи бота");
			}
		} while (await timer.WaitForNextTickAsync(stoppingToken));
	}
}
