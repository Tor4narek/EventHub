using MaxBotCore.Client;
using MaxBotCore.Configuration;
using MaxBotCore.Contracts.Attachments;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using System.Data.Common;
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
	private readonly ILogger<BotScenario>? _logger;

	public BotScenario(IUserService users, ITagService tags, IRecomendationService recommendations,
		IUserEventService savedEvents, IEventService events, IMaxBotClient max, IOptions<MaxOptions> options,
		ILogger<BotScenario>? logger = null)
	{
		_users = users;
		_tags = tags;
		_recommendations = recommendations;
		_savedEvents = savedEvents;
		_events = events;
		_max = max;
		_options = options.Value;
		_logger = logger;
	}

	public async Task HandleAsync(BotCommand command, CancellationToken cancellationToken)
	{
		if (command.MaxUserId <= 0) return;
		try { await HandleCommandAsync(command, cancellationToken); }
		catch (Exception ex) when (ex is DbException or DbUpdateException)
		{
			_logger?.LogError(ex, "Ошибка данных при выполнении команды {Command} пользователя MAX {UserId}", command.Type, command.MaxUserId);
			await PresentAsync(command, "Не удалось завершить действие. Проверь сохранённые мероприятия или попробуй ещё раз чуть позже.",
				[BotMessageFactory.NextActions(_options.WebAppName, true)], cancellationToken);
		}
	}

	private async Task HandleCommandAsync(BotCommand command, CancellationToken cancellationToken)
	{
		var user = await _users.CreateByMaxUserIdAsync(command.MaxUserId, cancellationToken);
		switch (command.Type)
		{
			case BotCommandType.Start:
				await SendAsync(command.MaxUserId, "Привет! Я EventHub. Помогу найти мероприятия по твоим интересам и напомню о сохранённых событиях.\n\nВ «Все мероприятия» можно открыть каталог с поиском и фильтрами. Регистрация проходит на сайте организатора.", null, cancellationToken);
				if (!user.HasCompletedOnboarding)
				{
					await ShowInterestsAsync(command.MaxUserId, user.Id, true, cancellationToken);
				}
				else await ShowMenuAsync(command.MaxUserId, cancellationToken);
				break;
			case BotCommandType.Menu:
				if (!user.HasCompletedOnboarding)
					await ShowInterestsAsync(command.MaxUserId, user.Id, true, cancellationToken, command.MessageId);
				else await ShowMenuAsync(command.MaxUserId, cancellationToken, command.MessageId);
				break;
			case BotCommandType.Help:
				await HelpAsync(command, cancellationToken);
				break;
			case BotCommandType.SetInterest:
				if (command.IsOnboarding && user.HasCompletedOnboarding)
				{
					await PresentAsync(command, "Этот выбор интересов уже завершён. Изменить их можно в разделе «Мои интересы».", [BotMessageFactory.NextActions(_options.WebAppName)], cancellationToken);
					break;
				}
				await SetInterestAsync(command, user.Id, cancellationToken);
				break;
			case BotCommandType.FinishOnboarding:
				if (await _users.CompleteOnboardingAsync(user.Id, cancellationToken))
				{
					await PresentAsync(command, "Интересы сохранены. Подбираю мероприятия на ближайшие семь дней.", [BotMessageFactory.Home()], cancellationToken);
					await ShowRecommendationsAsync(command.MaxUserId, user.Id, cancellationToken);
					await ShowMenuAsync(command.MaxUserId, cancellationToken);
				}
				else if ((await _users.GetUserTagIdsAsync(user.Id, cancellationToken)).Count == 0)
					await ShowInterestsAsync(command.MaxUserId, user.Id, true, cancellationToken, command.MessageId, "Выбери хотя бы один интерес.");
				else
					await ShowMenuAsync(command.MaxUserId, cancellationToken, command.MessageId);
				break;
			case BotCommandType.FindEvents:
				if (!user.HasCompletedOnboarding)
					await ShowInterestsAsync(command.MaxUserId, user.Id, true, cancellationToken);
				else await ShowRecommendationsAsync(command.MaxUserId, user.Id, cancellationToken);
				break;
			case BotCommandType.AllEvents:
				if (string.IsNullOrWhiteSpace(_options.WebAppName))
					await PresentAsync(command, "Каталог временно недоступен. Можно посмотреть подборку или сохранённые мероприятия.", [BotMessageFactory.NextActions(null, true)], cancellationToken);
				else await PresentAsync(command, "Открой каталог по кнопке «Все мероприятия ↗» ниже.", [BotMessageFactory.NextActions(_options.WebAppName)], cancellationToken);
				break;
			case BotCommandType.SavedEvents:
				await ShowSavedAsync(command.MaxUserId, user.Id, command.Offset, cancellationToken, command.Past);
				break;
			case BotCommandType.Interests:
				await ShowInterestsAsync(command.MaxUserId, user.Id, !user.HasCompletedOnboarding, cancellationToken, command.MessageId);
				break;
			case BotCommandType.Settings:
				await PresentAsync(command, DigestText(user.IsWeeklyDigestEnabled),
					[BotMessageFactory.WeeklyDigest(user.IsWeeklyDigestEnabled)], cancellationToken);
				break;
			case BotCommandType.SetWeeklyDigest:
				await _users.SetWeeklyDigestAsync(user.Id, command.Enabled!.Value, cancellationToken);
				await PresentAsync(command, DigestText(command.Enabled.Value),
					[BotMessageFactory.WeeklyDigest(command.Enabled.Value)], cancellationToken);
				break;
			case BotCommandType.Remind:
				await SaveAsync(command, user.Id, cancellationToken);
				break;
			case BotCommandType.RemoveReminder:
				await _savedEvents.RemoveEventAsync(user.Id, command.EntityId!.Value, cancellationToken);
				var removed = await FindPublishedAsync(command.EntityId.Value, cancellationToken);
				if (removed is not null)
				{
					var card = BotMessageFactory.EventCard(removed);
					await PresentAsync(command, card.Text + "\n\nУдалено из сохранённых. Напоминание выключено." +
						(removed.EventDateTime > DateTime.UtcNow ? " Можно сохранить снова." : ""), card.Attachments, cancellationToken);
				}
				else await PresentAsync(command, "Удалено из сохранённых. Напоминание выключено.", [BotMessageFactory.NextActions(_options.WebAppName)], cancellationToken);
				break;
			default:
				await PresentAsync(command, "Я понимаю команды и кнопки меню. Для поиска по названию открой «Все мероприятия».\n\n/menu — главное меню\n/help — помощь", [BotMessageFactory.MainMenu(_options.WebAppName)], cancellationToken);
				break;
		}
	}

	private async Task SetInterestAsync(BotCommand command, Guid userId, CancellationToken cancellationToken)
	{
		var selected = (await _users.GetUserTagIdsAsync(userId, cancellationToken)).ToHashSet();
		if (command.Enabled == true) selected.Add(command.EntityId!.Value);
		else selected.Remove(command.EntityId!.Value);
		if (selected.Count == 0 && !command.IsOnboarding)
		{
			await ShowInterestsAsync(command.MaxUserId, userId, false, cancellationToken, command.MessageId, "Оставь хотя бы один интерес, чтобы получать подборку.");
			return;
		}
		if (!(await _tags.GetTagsAsync(cancellationToken)).Any(tag => tag.Id == command.EntityId))
		{
			await ShowInterestsAsync(command.MaxUserId, userId, command.IsOnboarding, cancellationToken, command.MessageId, "Этот интерес больше недоступен. Выбери из актуального списка.");
			return;
		}
		await _users.UpdateUserTagsAsync(userId, selected, cancellationToken);
		await ShowInterestsAsync(command.MaxUserId, userId, command.IsOnboarding, cancellationToken, command.MessageId);
	}

	private async Task ShowInterestsAsync(long maxUserId, Guid userId, bool onboarding, CancellationToken cancellationToken,
		string? messageId = null, string? notice = null)
	{
		var tags = await _tags.GetTagsAsync(cancellationToken);
		if (tags.Count == 0)
		{
			await SendAsync(maxUserId, "Интересы временно недоступны. Можно открыть каталог всех мероприятий.", [BotMessageFactory.NextActions(_options.WebAppName)], cancellationToken);
			return;
		}
		var selected = await _users.GetUserTagIdsAsync(userId, cancellationToken);
		var names = tags.Where(tag => selected.Contains(tag.Id)).Select(tag => tag.Name);
		var text = (onboarding ? "Выбери интересы и нажми «Готово». Нужно выбрать хотя бы один. Изменить выбор можно позже." : "Мои интересы. Изменения сохраняются сразу.") +
			"\n\nВыбрано: " + (selected.Count == 0 ? "пока ничего" : string.Join(", ", names));
		if (notice is not null) text = notice + "\n\n" + text;
		await PresentAsync(new BotCommand(maxUserId, BotCommandType.Interests, MessageId: messageId), text,
			[BotMessageFactory.Interests(tags, selected, onboarding)], cancellationToken);
	}

	private async Task ShowRecommendationsAsync(long maxUserId, Guid userId, CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		var events = await _recommendations.GetTopEventsAsync(userId, 3, now, now.AddDays(7), cancellationToken);
		if (events.Count == 0)
		{
			var hasSaved = (await _savedEvents.GetSavedEventsAsync(userId, cancellationToken)).Any(item => item.EventDateTime > now);
			await SendAsync(maxUserId, "Новых мероприятий с открытой регистрацией на ближайшие 7 дней пока нет. " +
				(hasSaved ? "Посмотри сохранённые события или открой каталог." : "Открой каталог всех мероприятий или измени интересы."),
				[BotMessageFactory.NextActions(_options.WebAppName, hasSaved)], cancellationToken);
			return;
		}
		await SendAsync(maxUserId, $"Подборка на ближайшие 7 дней · мероприятий: {events.Count}", null, cancellationToken);
		await SendEventsAsync(maxUserId, events, false, cancellationToken);
		await SendAsync(maxUserId, "Сохранение включает напоминание. Регистрация — на сайте организатора.", [BotMessageFactory.NextActions(_options.WebAppName, true)], cancellationToken);
	}

	private async Task ShowSavedAsync(long maxUserId, Guid userId, int offset, CancellationToken cancellationToken, bool past = false)
	{
		var saved = await _savedEvents.GetSavedEventsAsync(userId, cancellationToken);
		var now = DateTime.UtcNow;
		var events = saved.Where(item => past ? item.EventDateTime <= now : item.EventDateTime > now).ToList();
		if (events.Count == 0)
		{
			await SendAsync(maxUserId, past ? "Прошедших сохранённых мероприятий нет." : "Предстоящих сохранённых мероприятий пока нет. Сохрани событие из подборки или каталога — я напомню о нём.",
				[past ? BotMessageFactory.SavedNavigation(0, 0, true, false) :
					BotMessageFactory.NextActions(_options.WebAppName, hasPast: saved.Any(item => item.EventDateTime <= now))], cancellationToken);
			return;
		}
		const int pageSize = 5;
		var page = events.Skip(offset).Take(pageSize).ToList();
		if (page.Count == 0)
		{
			await SendAsync(maxUserId, "Список изменился. Открой сохранённые заново.", [BotMessageFactory.NextActions(_options.WebAppName, true)], cancellationToken);
			return;
		}
		await SendAsync(maxUserId, $"{(past ? "Прошедшие" : "Предстоящие")} сохранённые · {offset + 1}–{offset + page.Count} из {events.Count}", null, cancellationToken);
		await SendEventsAsync(maxUserId, page, true, cancellationToken);
		await SendAsync(maxUserId, "Что дальше?", [BotMessageFactory.SavedNavigation(offset, events.Count, past, saved.Any(item => item.EventDateTime <= now))], cancellationToken);
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

	private Task ShowMenuAsync(long maxUserId, CancellationToken cancellationToken, string? messageId = null) =>
		PresentAsync(new BotCommand(maxUserId, BotCommandType.Menu, MessageId: messageId), "Главное меню", [BotMessageFactory.MainMenu(_options.WebAppName)], cancellationToken);

	private static string DigestText(bool enabled) =>
		"Еженедельная подборка: " + (enabled ? "включена" : "выключена") +
		"\n\nКаждое воскресенье — мероприятия на следующую неделю по твоим интересам. Эта настройка не отключает напоминания о сохранённых событиях.";

	private Task HelpAsync(BotCommand command, CancellationToken cancellationToken) =>
		PresentAsync(command, "Как пользоваться EventHub\n\nПодборка — до трёх новых мероприятий на ближайшие 7 дней по твоим интересам.\nВсе мероприятия — каталог с поиском и фильтрами внутри MAX.\nСохранить и напомнить — добавить событие в сохранённые и включить напоминание примерно за сутки. Если осталось меньше суток, оно придёт в ближайшее время до начала события.\nРегистрация проходит на сайте организатора. Сохранение не регистрирует тебя на мероприятие.\n\n/menu — меню\n/find — подборка\n/events — каталог\n/saved — сохранённые\n/interests — интересы\n/settings — рассылка\n/help — помощь", [BotMessageFactory.Home()], cancellationToken);

	private async Task<Event?> FindPublishedAsync(Guid id, CancellationToken cancellationToken)
	{
		try { return await _events.GetEventByIdAsync(id, cancellationToken, EventStatus.Published); }
		catch (KeyNotFoundException) { return null; }
	}

	private async Task SaveAsync(BotCommand command, Guid userId, CancellationToken cancellationToken)
	{
		var item = await FindPublishedAsync(command.EntityId!.Value, cancellationToken);
		if (item is null)
		{
			await PresentAsync(command, "Это мероприятие больше недоступно. Посмотри актуальные события.", [BotMessageFactory.NextActions(_options.WebAppName, true)], cancellationToken);
			return;
		}
		var alreadySaved = await _savedEvents.IsEventSavedAsync(userId, item.Id, cancellationToken);
		try { await _savedEvents.SaveEventAsync(userId, item.Id, cancellationToken); }
		catch (KeyNotFoundException)
		{
			await PresentAsync(command, "Это мероприятие больше недоступно. Посмотри актуальные события.", [BotMessageFactory.NextActions(_options.WebAppName, true)], cancellationToken);
			return;
		}
		catch (InvalidOperationException ex) when (ex.Message == "Можно сохранить только опубликованное предстоящее мероприятие.")
		{
			await PresentAsync(command, "Мероприятие уже началось или больше недоступно. Напоминание не включено.", [BotMessageFactory.NextActions(_options.WebAppName, true)], cancellationToken);
			return;
		}
		var card = BotMessageFactory.EventCard(item, true);
		var confirmation = alreadySaved ? "Уже в сохранённых." :
			item.EventDateTime - DateTime.UtcNow < TimeSpan.FromDays(1)
				? "Сохранено. До начала меньше суток — напоминание придёт в ближайшее время."
				: "Сохранено. Напомню примерно за сутки до начала.";
		await PresentAsync(command, card.Text + "\n\n" + confirmation, card.Attachments, cancellationToken);
	}

	private async Task PresentAsync(BotCommand command, string text, IReadOnlyList<MaxAttachment> attachments,
		CancellationToken cancellationToken)
	{
		if (!string.IsNullOrWhiteSpace(command.MessageId))
		{
			// Очередь бота обрабатывается последовательно; задержка соблюдает лимит MAX на редактирование.
			await Task.Delay(TimeSpan.FromMilliseconds(550), cancellationToken);
			try
			{
				await _max.EditMessageAsync(command.MessageId, text, attachments, cancellationToken);
				return;
			}
			catch (HttpRequestException ex) { _logger?.LogWarning(ex, "Не удалось обновить сообщение MAX {MessageId}", command.MessageId); }
			catch (KeyNotFoundException ex) { _logger?.LogWarning(ex, "Сообщение MAX {MessageId} больше недоступно", command.MessageId); }
			catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
			{ _logger?.LogWarning(ex, "Истёк тайм-аут обновления сообщения MAX {MessageId}", command.MessageId); }
		}
		await SendAsync(command.MaxUserId, text, attachments, cancellationToken);
	}

	private async Task SendAsync(long userId, string text, IReadOnlyList<MaxAttachment>? attachments,
		CancellationToken cancellationToken) =>
		await _max.SendMessageToUserAsync(userId, text, attachments, notify: false, cancellationToken: cancellationToken);
}
