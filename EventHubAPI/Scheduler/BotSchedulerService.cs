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
		using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
		do
		{
			try
			{
				await using var scope = _scopeFactory.CreateAsyncScope();
				var now = DateTime.UtcNow;
				await scope.ServiceProvider.GetRequiredService<ReminderJob>().RunAsync(now, stoppingToken);
				await scope.ServiceProvider.GetRequiredService<WeeklyDigestJob>().RunAsync(now, stoppingToken);
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
