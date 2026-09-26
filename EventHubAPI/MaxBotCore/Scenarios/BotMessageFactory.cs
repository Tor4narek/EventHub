using MaxBotCore.Contracts.Attachments;
using MaxBotCore.Contracts.Attachments.Buttons;
using Storage.Entities;

namespace MaxBotCore.Scenarios;

public static class BotMessageFactory
{
	private static readonly TimeZoneInfo Moscow = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");

	public static InlineKeyboardAttachment MainMenu(string? webAppName = null)
	{
		var allEvents = !string.IsNullOrWhiteSpace(webAppName)
			? (MaxButton)new OpenAppButton { Text = "Все мероприятия", WebApp = webAppName }
			: new CallbackButton { Text = "Все мероприятия", Payload = "menu:all" };
		return Keyboard(
			[new CallbackButton { Text = "Подобрать мероприятия", Payload = "menu:find" }],
			[allEvents],
			[new CallbackButton { Text = "Сохранённые", Payload = "menu:saved" }],
			[new CallbackButton { Text = "Мои интересы", Payload = "menu:interests" }],
			[new CallbackButton { Text = "Настройки", Payload = "menu:settings" }],
			[new CallbackButton { Text = "Помощь", Payload = "menu:help" }]);
	}

	public static InlineKeyboardAttachment Interests(IReadOnlyList<Tag> tags, IReadOnlyCollection<Guid> selected, bool onboarding)
	{
		var selectedIds = selected.ToHashSet();
		var rows = tags.Select(tag => (IReadOnlyList<MaxButton>)
			[new CallbackButton
			{
				Text = $"{(selectedIds.Contains(tag.Id) ? "✅ " : "")}{tag.Name}",
				Payload = $"interest:{(onboarding ? "on" : "edit")}:{(selectedIds.Contains(tag.Id) ? "remove" : "add")}:{tag.Id}"
			}]).ToList();
		rows.Add([new CallbackButton
		{
			Text = onboarding ? $"Готово · выбрано {selected.Count}" : "Готово · в меню",
			Payload = onboarding ? "onboarding:done" : "menu:home"
		}]);
		return new InlineKeyboardAttachment { Payload = new InlineKeyboardPayload { Buttons = rows } };
	}

	public static (string Text, IReadOnlyList<MaxAttachment> Attachments) EventCard(Event item, bool saved = false)
	{
		var date = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(item.EventDateTime, DateTimeKind.Utc), Moscow);
		var deadline = item.Deadline is { } value
			? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), Moscow).ToString("dd.MM.yyyy HH:mm") + " МСК"
			: "не указан";
		var registrationClosed = item.Deadline is { } registrationDeadline && registrationDeadline <= DateTime.UtcNow;
		var past = item.EventDateTime <= DateTime.UtcNow;
		var text = $"{item.Title}\n{date:dd.MM.yyyy HH:mm} МСК · {item.Location}\n\n{Shorten(item.Description, 280)}\n\n" +
			(past ? "Мероприятие уже началось или завершилось." : registrationClosed ? "Срок регистрации закончился." :
				item.Deadline.HasValue ? $"Регистрация до: {deadline}" : "Дедлайн регистрации не указан.") +
			(saved ? "\n✓ Сохранено" : "");
		var buttons = new List<MaxButton>();
		if (Uri.TryCreate(item.Source, UriKind.Absolute, out var source) && source.Scheme == Uri.UriSchemeHttps)
			buttons.Add(new LinkButton { Text = past || registrationClosed ? "Сайт организатора ↗" : "Зарегистрироваться ↗", Url = item.Source });
		if (saved || !past) buttons.Add(new CallbackButton
		{
			Text = saved ? "Убрать из сохранённых" : "🔔 Сохранить и напомнить",
			Payload = $"{(saved ? "unsave" : "remind")}:{item.Id}"
		});
		var attachments = new List<MaxAttachment>();
		if (Uri.TryCreate(item.MainImg, UriKind.Absolute, out var image) && image.Scheme == Uri.UriSchemeHttps)
			attachments.Add(new ImageAttachment { Payload = new ImageAttachmentPayload { Url = item.MainImg } });
		attachments.Add(Keyboard(buttons.ToArray()));
		return (text, attachments);
	}

	public static InlineKeyboardAttachment WeeklyDigest(bool enabled) => Keyboard(
		[new CallbackButton { Text = enabled ? "Выключить рассылку" : "Включить рассылку", Payload = enabled ? "digest:off" : "digest:on" }],
		[new CallbackButton { Text = "Главное меню", Payload = "menu:home" }]);

	public static InlineKeyboardAttachment MoreSaved(int offset) => Keyboard(
		[new CallbackButton { Text = "Показать ещё", Payload = $"saved:{offset}" }]);

	public static InlineKeyboardAttachment SavedEvents() => Keyboard(
		[new CallbackButton { Text = "Сохранённые", Payload = "menu:saved" }]);

	public static InlineKeyboardAttachment Home() => Keyboard(
		[new CallbackButton { Text = "Главное меню", Payload = "menu:home" }]);

	public static InlineKeyboardAttachment NextActions(string? webAppName, bool saved = false, bool hasPast = false)
	{
		var rows = new List<IReadOnlyList<MaxButton>>();
		if (saved) rows.Add([new CallbackButton { Text = "Сохранённые", Payload = "menu:saved" }]);
		if (hasPast) rows.Add([new CallbackButton { Text = "Прошедшие сохранённые", Payload = "saved:past" }]);
		rows.Add([new CallbackButton { Text = "Подобрать мероприятия", Payload = "menu:find" }]);
		if (!string.IsNullOrWhiteSpace(webAppName))
			rows.Add([new OpenAppButton { Text = "Все мероприятия ↗", WebApp = webAppName }]);
		rows.Add([new CallbackButton { Text = "Мои интересы", Payload = "menu:interests" }]);
		rows.Add([new CallbackButton { Text = "Главное меню", Payload = "menu:home" }]);
		return new InlineKeyboardAttachment { Payload = new InlineKeyboardPayload { Buttons = rows } };
	}

	public static InlineKeyboardAttachment SavedNavigation(int offset, int count, bool past, bool hasPast)
	{
		var rows = new List<IReadOnlyList<MaxButton>>();
		var prefix = past ? "saved:past:" : "saved:";
		if (offset > 0) rows.Add([new CallbackButton { Text = "Назад", Payload = prefix + Math.Max(0, offset - 5) }]);
		if (count > offset + 5) rows.Add([new CallbackButton { Text = "Показать ещё", Payload = prefix + (offset + 5) }]);
		if (past) rows.Add([new CallbackButton { Text = "Предстоящие", Payload = "menu:saved" }]);
		else if (hasPast) rows.Add([new CallbackButton { Text = "Прошедшие", Payload = "saved:past" }]);
		rows.Add([new CallbackButton { Text = "Главное меню", Payload = "menu:home" }]);
		return new InlineKeyboardAttachment { Payload = new InlineKeyboardPayload { Buttons = rows } };
	}

	private static InlineKeyboardAttachment Keyboard(params IReadOnlyList<MaxButton>[] rows) =>
		new() { Payload = new InlineKeyboardPayload { Buttons = rows } };

	private static string Shorten(string? text, int max) =>
		string.IsNullOrWhiteSpace(text) ? "Описание отсутствует" :
		text.Length > max ? text[..max].TrimEnd() + "…" : text;
}
