using MaxBotCore.Client;
using MaxBotCore.Configuration;
using MaxBotCore.Contracts.Attachments;
using Microsoft.Extensions.Options;
using Services.Interfaces;
using Storage.Entities;

namespace MaxBotCore.Scenarios;

public interface IBotScenario
{
	Task HandleAsync(BotCommand command, CancellationToken cancellationToken);
}

public sealed class BotScenario : IBotScenario
{
	private readonly IUserService _users;
	private readonly ITagService _tags;
	private readonly IRecomendationService _recommendations;
	private readonly IUserEventService _savedEvents;
	private readonly IEventService _events;
	private readonly IMaxBotClient _max;
	private readonly MaxOptions _options;

	public BotScenario(IUserService users, ITagService tags, IRecomendationService recommendations,
		IUserEventService savedEvents, IEventService events, IMaxBotClient max, IOptions<MaxOptions> options)
	{
		_users = users;
		_tags = tags;
		_recommendations = recommendations;
		_savedEvents = savedEvents;
		_events = events;
		_max = max;
		_options = options.Value;
	}

	public async Task HandleAsync(BotCommand command, CancellationToken cancellationToken)
	{
		if (command.MaxUserId <= 0) return;
		var user = await _users.CreateByMaxUserIdAsync(command.MaxUserId, cancellationToken);
		switch (command.Type)
		{
			case BotCommandType.Start:
				if (!user.HasCompletedOnboarding)
				{
					await SendAsync(command.MaxUserId, "Привет! Выбери интересы, чтобы я подобрал мероприятия.", null, cancellationToken);
					await ShowInterestsAsync(command.MaxUserId, user.Id, true, cancellationToken);
				}
				else await ShowMenuAsync(command.MaxUserId, cancellationToken);
				break;
			case BotCommandType.SetInterest:
				await SetInterestAsync(command, user.Id, cancellationToken);
				break;
			case BotCommandType.FinishOnboarding:
				if (await _users.CompleteOnboardingAsync(user.Id, cancellationToken))
				{
					await ShowRecommendationsAsync(command.MaxUserId, user.Id, cancellationToken);
					await ShowMenuAsync(command.MaxUserId, cancellationToken);
				}
				else if ((await _users.GetUserTagIdsAsync(user.Id, cancellationToken)).Count == 0)
					await SendAsync(command.MaxUserId, "Выбери хотя бы один интерес.", null, cancellationToken);
				else
					await ShowMenuAsync(command.MaxUserId, cancellationToken);
				break;
			case BotCommandType.FindEvents:
				if (!user.HasCompletedOnboarding)
					await ShowInterestsAsync(command.MaxUserId, user.Id, true, cancellationToken);
				else await ShowRecommendationsAsync(command.MaxUserId, user.Id, cancellationToken);
				break;
			case BotCommandType.AllEvents:
				await ShowMenuAsync(command.MaxUserId, cancellationToken);
				if (string.IsNullOrWhiteSpace(_options.WebAppName))
					await SendAsync(command.MaxUserId, "Мини-приложение пока не подключено к боту.", null, cancellationToken);
				break;
			case BotCommandType.SavedEvents:
				await ShowSavedAsync(command.MaxUserId, user.Id, command.Offset, cancellationToken);
				break;
			case BotCommandType.Interests:
				await ShowInterestsAsync(command.MaxUserId, user.Id, !user.HasCompletedOnboarding, cancellationToken);
				break;
			case BotCommandType.Settings:
				await SendAsync(command.MaxUserId, "Еженедельная подборка: " +
					(user.IsWeeklyDigestEnabled ? "включена" : "выключена"),
					[BotMessageFactory.WeeklyDigest(user.IsWeeklyDigestEnabled)], cancellationToken);
				break;
			case BotCommandType.SetWeeklyDigest:
				await _users.SetWeeklyDigestAsync(user.Id, command.Enabled!.Value, cancellationToken);
				await SendAsync(command.MaxUserId, command.Enabled.Value ? "Рассылка включена." : "Рассылка выключена.",
					[BotMessageFactory.WeeklyDigest(command.Enabled.Value)], cancellationToken);
				break;
			case BotCommandType.Remind:
				await _events.GetEventByIdAsync(command.EntityId!.Value, cancellationToken, EventStatus.Published);
				await _savedEvents.SaveEventAsync(user.Id, command.EntityId.Value, cancellationToken);
				await SendAsync(command.MaxUserId, "Мероприятие сохранено. Напомню примерно за сутки до начала. Если осталось меньше суток — после ближайшей проверки, пока мероприятие не началось.", null, cancellationToken);
				break;
			case BotCommandType.RemoveReminder:
				await _savedEvents.RemoveEventAsync(user.Id, command.EntityId!.Value, cancellationToken);
				await SendAsync(command.MaxUserId, "Напоминание удалено.", null, cancellationToken);
				break;
			default:
				await ShowMenuAsync(command.MaxUserId, cancellationToken);
				break;
		}
	}

	private async Task SetInterestAsync(BotCommand command, Guid userId, CancellationToken cancellationToken)
	{
		var selected = (await _users.GetUserTagIdsAsync(userId, cancellationToken)).ToHashSet();
		if (command.Enabled == true) selected.Add(command.EntityId!.Value);
		else selected.Remove(command.EntityId!.Value);
		await _users.UpdateUserTagsAsync(userId, selected, cancellationToken);
		await ShowInterestsAsync(command.MaxUserId, userId, command.IsOnboarding, cancellationToken);
	}

	private async Task ShowInterestsAsync(long maxUserId, Guid userId, bool onboarding, CancellationToken cancellationToken)
	{
		var tags = await _tags.GetTagsAsync(cancellationToken);
		if (tags.Count == 0)
		{
			await SendAsync(maxUserId, "Доступных интересов пока нет.", null, cancellationToken);
			return;
		}
		var selected = await _users.GetUserTagIdsAsync(userId, cancellationToken);
		await SendAsync(maxUserId, onboarding ? "Выбери интересы и нажми «Готово»." : "Измени свои интересы:",
			[BotMessageFactory.Interests(tags, selected, onboarding)], cancellationToken);
	}

	private async Task ShowRecommendationsAsync(long maxUserId, Guid userId, CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		var events = await _recommendations.GetTopEventsAsync(userId, 3, now, now.AddDays(7), cancellationToken);
		if (events.Count == 0)
		{
			await SendAsync(maxUserId, "Новых мероприятий на ближайшие семь дней пока нет. Сохранённые мероприятия можно посмотреть по кнопке ниже.",
				[BotMessageFactory.SavedEvents()], cancellationToken);
			return;
		}
		await SendEventsAsync(maxUserId, events, false, cancellationToken);
	}

	private async Task ShowSavedAsync(long maxUserId, Guid userId, int offset, CancellationToken cancellationToken)
	{
		var events = await _savedEvents.GetSavedEventsAsync(userId, cancellationToken);
		if (events.Count == 0)
		{
			await SendAsync(maxUserId, "Сохранённых мероприятий пока нет.", null, cancellationToken);
			return;
		}
		const int pageSize = 5;
		var page = events.Skip(offset).Take(pageSize).ToList();
		if (page.Count == 0)
		{
			await SendAsync(maxUserId, "Больше сохранённых мероприятий нет.", null, cancellationToken);
			return;
		}
		await SendEventsAsync(maxUserId, page, true, cancellationToken);
		if (events.Count > offset + page.Count)
			await SendAsync(maxUserId, "Следующие сохранённые мероприятия:",
				[BotMessageFactory.MoreSaved(offset + page.Count)], cancellationToken);
	}

	private async Task SendEventsAsync(long maxUserId, IReadOnlyList<Event> events, bool saved, CancellationToken cancellationToken)
	{
		foreach (var item in events)
		{
			var card = BotMessageFactory.EventCard(item, saved);
			await SendAsync(maxUserId, card.Text, card.Attachments, cancellationToken);
			await Task.Delay(TimeSpan.FromMilliseconds(550), cancellationToken);
		}
	}

	private Task ShowMenuAsync(long maxUserId, CancellationToken cancellationToken) =>
		SendAsync(maxUserId, "Главное меню", [BotMessageFactory.MainMenu(_options.WebAppName)], cancellationToken);

	private async Task SendAsync(long userId, string text, IReadOnlyList<MaxAttachment>? attachments,
		CancellationToken cancellationToken) =>
		await _max.SendMessageToUserAsync(userId, text, attachments, cancellationToken: cancellationToken);
}
