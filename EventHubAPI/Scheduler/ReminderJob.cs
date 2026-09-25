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
		var from = utcNow;
		var to = utcNow.AddDays(1);
		while (true)
		{
			var batch = await _savedEvents.GetDueRemindersAsync(from, to, 100, cancellationToken);
			if (batch.Count == 0) break;
			foreach (var saved in batch)
			{
				var moscow = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
				var localNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, moscow);
				var localTime = TimeZoneInfo.ConvertTimeFromUtc(
					DateTime.SpecifyKind(saved.Event.EventDateTime, DateTimeKind.Utc), moscow);
				var day = localTime.Date == localNow.Date ? "сегодня" : "завтра";
				var claimedAt = DateTime.UtcNow;
				if (!await _savedEvents.TryClaimReminderAsync(saved.UserId, saved.EventId, claimedAt, cancellationToken)) continue;
				try
				{
					await _max.SendMessageToUserAsync(saved.User.MaxUserId,
						$"Напоминаю: {day} в {localTime:HH:mm} МСК состоится «{saved.Event.Title}».\n{saved.Event.Source}",
						cancellationToken: CancellationToken.None);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					throw;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Статус доставки напоминания {EventId} пользователю {UserId} неизвестен; повтор не выполняется",
						saved.EventId, saved.UserId);
				}
			}
			if (batch.Count < 100) break;
		}
	}
}
