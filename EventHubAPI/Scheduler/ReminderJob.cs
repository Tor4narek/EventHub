using MaxBotCore.Client;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Scheduler;

public sealed class ReminderJob
{
	private readonly IUserEventService _savedEvents;
	private readonly IMaxBotClient _max;
	private readonly ILogger<ReminderJob> _logger;

	public ReminderJob(IUserEventService savedEvents, IMaxBotClient max, ILogger<ReminderJob> logger)
	{
		_savedEvents = savedEvents;
		_max = max;
		_logger = logger;
	}

	public async Task RunAsync(DateTime utcNow, CancellationToken cancellationToken)
	{
		var (from, to) = BotSchedule.Tomorrow(utcNow);
		while (true)
		{
			var batch = await _savedEvents.GetDueRemindersAsync(from, to, 100, cancellationToken);
			if (batch.Count == 0) break;
			var failed = false;
			foreach (var saved in batch)
			{
				var claimedAt = DateTime.UtcNow;
				if (!await _savedEvents.TryClaimReminderAsync(saved.UserId, saved.EventId, claimedAt, cancellationToken)) continue;
				try
				{
					var localTime = TimeZoneInfo.ConvertTimeFromUtc(
						DateTime.SpecifyKind(saved.Event.EventDateTime, DateTimeKind.Utc),
						TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"));
					await _max.SendMessageToUserAsync(saved.User.MaxUserId,
						$"Напоминаю: завтра в {localTime:HH:mm} МСК состоится «{saved.Event.Title}».\n{saved.Event.Source}",
						cancellationToken: cancellationToken);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					throw;
				}
				catch (Exception ex)
				{
					await _savedEvents.ReleaseReminderClaimAsync(saved.UserId, saved.EventId, claimedAt, cancellationToken);
					_logger.LogError(ex, "Не удалось отправить напоминание {EventId} пользователю {UserId}",
						saved.EventId, saved.UserId);
					failed = true;
				}
			}
			if (failed || batch.Count < 100) break;
		}
	}
}
