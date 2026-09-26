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
			[new CallbackButton { Text = "Настройки", Payload = "menu:settings" }]);
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
			Text = onboarding ? "Готово" : "В меню",
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
		var text = $"{item.Title}\n\n{Shorten(item.Description, 500)}\n\n{date:dd.MM.yyyy HH:mm} МСК\n{item.Location}\nДедлайн регистрации: {deadline}\nИсточник: {item.Source}";
		var buttons = new List<MaxButton>();
		if (Uri.TryCreate(item.Source, UriKind.Absolute, out var source) && source.Scheme == Uri.UriSchemeHttps)
			buttons.Add(new LinkButton { Text = "Зарегистрироваться", Url = item.Source });
		buttons.Add(new CallbackButton
		{
			Text = saved ? "Убрать из сохранённых" : "🔔 Напомнить",
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

	private static InlineKeyboardAttachment Keyboard(params IReadOnlyList<MaxButton>[] rows) =>
		new() { Payload = new InlineKeyboardPayload { Buttons = rows } };

	private static string Shorten(string? text, int max) =>
		string.IsNullOrWhiteSpace(text) ? "Описание отсутствует" :
		text.Length > max ? text[..max].TrimEnd() + "…" : text;
}
