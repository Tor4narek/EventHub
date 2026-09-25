using MaxBotCore.Client;
using MaxBotCore.Scenarios;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Scheduler;

public sealed class WeeklyDigestJob
{
	private readonly IUserService _users;
	private readonly IRecomendationService _recommendations;
	private readonly IMaxBotClient _max;
	private readonly ILogger<WeeklyDigestJob> _logger;

	public WeeklyDigestJob(IUserService users, IRecomendationService recommendations,
		IMaxBotClient max, ILogger<WeeklyDigestJob> logger)
	{
		_users = users;
		_recommendations = recommendations;
		_max = max;
		_logger = logger;
	}

	public async Task RunAsync(DateTime utcNow, CancellationToken cancellationToken)
	{
		if (!BotSchedule.IsSunday(utcNow)) return;
		var (from, to) = BotSchedule.NextWeek(utcNow);
		var weekStart = BotSchedule.MoscowDayStartUtc(utcNow);
		while (true)
		{
			var batch = await _users.GetWeeklyDigestSubscribersAsync(weekStart, 100, cancellationToken);
			if (batch.Count == 0) break;
			var failed = false;
			foreach (var user in batch)
			{
				var claimedAt = DateTime.UtcNow;
				if (!await _users.TryClaimWeeklyDigestAsync(user.Id, weekStart, claimedAt, cancellationToken)) continue;
				var sendStarted = false;
				try
				{
					var events = await _recommendations.GetTopEventsAsync(user.Id, 3, from, to, cancellationToken);
					if (events.Count == 0) continue;
					sendStarted = true;
					await _max.SendMessageToUserAsync(user.MaxUserId, "Подборка мероприятий на следующую неделю:",
						cancellationToken: CancellationToken.None);
					foreach (var item in events)
					{
						await Task.Delay(TimeSpan.FromMilliseconds(550), CancellationToken.None);
						var card = BotMessageFactory.EventCard(item);
						await _max.SendMessageToUserAsync(user.MaxUserId, card.Text, card.Attachments,
							cancellationToken: CancellationToken.None);
					}
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					if (!sendStarted)
						await _users.ReleaseWeeklyDigestClaimAsync(user.Id, claimedAt, CancellationToken.None);
					throw;
				}
				catch (Exception ex)
				{
					if (!sendStarted)
						await _users.ReleaseWeeklyDigestClaimAsync(user.Id, claimedAt, cancellationToken);
					_logger.LogError(ex, "Не удалось подготовить или отправить недельную подборку пользователю {UserId}; повтор после начала отправки не выполняется", user.Id);
					failed = true;
				}
			}
			if (failed || batch.Count < 100) break;
		}
	}
}
