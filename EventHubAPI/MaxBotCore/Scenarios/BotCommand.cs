namespace MaxBotCore.Scenarios;

public enum BotCommandType
{
	Unknown,
	Start,
	FindEvents,
	AllEvents,
	SavedEvents,
	Interests,
	Settings,
	SetInterest,
	FinishOnboarding,
	Remind,
	RemoveReminder,
	SetWeeklyDigest
}

public sealed record BotCommand(long MaxUserId, BotCommandType Type, Guid? EntityId = null,
	bool? Enabled = null, bool IsOnboarding = false, int Offset = 0);

public static class BotCommandParser
{
	public static BotCommand ParseCallback(long maxUserId, string? payload)
	{
		if (string.IsNullOrWhiteSpace(payload)) return new(maxUserId, BotCommandType.Unknown);
		return payload switch
		{
			"menu:find" => new(maxUserId, BotCommandType.FindEvents),
			"menu:all" => new(maxUserId, BotCommandType.AllEvents),
			"menu:saved" => new(maxUserId, BotCommandType.SavedEvents),
			"menu:interests" => new(maxUserId, BotCommandType.Interests),
			"menu:settings" => new(maxUserId, BotCommandType.Settings),
			"menu:home" => new(maxUserId, BotCommandType.Start),
			"onboarding:done" => new(maxUserId, BotCommandType.FinishOnboarding),
			"digest:on" => new(maxUserId, BotCommandType.SetWeeklyDigest, Enabled: true),
			"digest:off" => new(maxUserId, BotCommandType.SetWeeklyDigest, Enabled: false),
			_ when payload.StartsWith("saved:", StringComparison.Ordinal) &&
				int.TryParse(payload[6..], out var offset) && offset >= 0 && offset <= 10000
				=> new(maxUserId, BotCommandType.SavedEvents, Offset: offset),
			_ => ParseEntityCommand(maxUserId, payload)
		};
	}

	public static BotCommand ParseText(long maxUserId, string? text) => text?.Trim().ToLowerInvariant() switch
	{
		"/start" => new(maxUserId, BotCommandType.Start),
		"подобрать мероприятия" => new(maxUserId, BotCommandType.FindEvents),
		"все мероприятия" => new(maxUserId, BotCommandType.AllEvents),
		"сохранённые" or "сохраненные" => new(maxUserId, BotCommandType.SavedEvents),
		"мои интересы" => new(maxUserId, BotCommandType.Interests),
		"настройки" => new(maxUserId, BotCommandType.Settings),
		_ => new(maxUserId, BotCommandType.Unknown)
	};

	private static BotCommand ParseEntityCommand(long maxUserId, string payload)
	{
		var separator = payload.LastIndexOf(':');
		if (separator < 1 || !Guid.TryParse(payload[(separator + 1)..], out var id) || id == Guid.Empty)
			return new(maxUserId, BotCommandType.Unknown);
		return payload[..separator] switch
		{
			"interest:on:add" => new(maxUserId, BotCommandType.SetInterest, id, true, true),
			"interest:on:remove" => new(maxUserId, BotCommandType.SetInterest, id, false, true),
			"interest:edit:add" => new(maxUserId, BotCommandType.SetInterest, id, true),
			"interest:edit:remove" => new(maxUserId, BotCommandType.SetInterest, id, false),
			"remind" => new(maxUserId, BotCommandType.Remind, id),
			"unsave" => new(maxUserId, BotCommandType.RemoveReminder, id),
			_ => new(maxUserId, BotCommandType.Unknown)
		};
	}
}
