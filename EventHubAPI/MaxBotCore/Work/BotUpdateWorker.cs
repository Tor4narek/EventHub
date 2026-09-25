using System.Text.Json;
using MaxBotCore.Contracts.Serialization;
using MaxBotCore.Contracts.Updates;
using MaxBotCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace MaxBotCore.Work;

public sealed class BotUpdateWorker : BackgroundService
{
	private readonly IServiceScopeFactory _scopeFactory;
	private readonly ILogger<BotUpdateWorker> _logger;
	private DateTime _lastCleanupAt = DateTime.MinValue;

	public BotUpdateWorker(IServiceScopeFactory scopeFactory, ILogger<BotUpdateWorker> logger)
	{
		_scopeFactory = scopeFactory;
		_logger = logger;
	}

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				await ProcessBatchAsync(stoppingToken);
				await timer.WaitForNextTickAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Не удалось обработать очередь обновлений MAX");
				await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
			}
		}
	}

	public async Task ProcessBatchAsync(CancellationToken cancellationToken)
	{
		await using var readScope = _scopeFactory.CreateAsyncScope();
		var inbox = readScope.ServiceProvider.GetRequiredService<IBotUpdateInboxService>();
		if (DateTime.UtcNow - _lastCleanupAt >= TimeSpan.FromHours(1))
		{
			await inbox.PurgeProcessedBeforeAsync(DateTime.UtcNow.AddDays(-7), cancellationToken);
			_lastCleanupAt = DateTime.UtcNow;
		}
		var updates = await inbox.GetAvailableAsync(DateTime.UtcNow, 20, cancellationToken);
		foreach (var item in updates)
		{
			await using var scope = _scopeFactory.CreateAsyncScope();
			var scopedInbox = scope.ServiceProvider.GetRequiredService<IBotUpdateInboxService>();
			var now = DateTime.UtcNow;
			if (!await scopedInbox.TryClaimAsync(item.Id, now, now.AddMinutes(10), cancellationToken)) continue;
			try
			{
				var update = JsonSerializer.Deserialize<MaxUpdate>(item.Payload, MaxJsonSerializerOptions.Default)
					?? throw new JsonException("Пустое обновление MAX.");
				await scope.ServiceProvider.GetRequiredService<IMaxUpdateRouter>()
					.RouteAsync(update, cancellationToken);
				await scopedInbox.MarkProcessedAsync(item.Id, DateTime.UtcNow, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				var delay = TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Min(item.AttemptCount + 1, 8))));
				await scopedInbox.RecordFailureAsync(item.Id, DateTime.UtcNow.Add(delay), cancellationToken);
				_logger.LogError(ex, "Ошибка обработки обновления MAX {UpdateId}, попытка {Attempt}",
					item.Id, item.AttemptCount + 1);
			}
		}
	}
}
