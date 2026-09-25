namespace Scheduler;

public static class BotSchedule
{
	private static readonly TimeZoneInfo Moscow = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");

	public static bool IsSunday(DateTime utcNow) =>
		TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(utcNow), Moscow).DayOfWeek == DayOfWeek.Sunday;

	public static (DateTime From, DateTime To) NextWeek(DateTime utcNow)
	{
		var local = TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(utcNow), Moscow);
		var daysUntilMonday = ((int)DayOfWeek.Monday - (int)local.DayOfWeek + 7) % 7;
		if (daysUntilMonday == 0) daysUntilMonday = 7;
		var from = local.Date.AddDays(daysUntilMonday);
		return (TimeZoneInfo.ConvertTimeToUtc(from, Moscow), TimeZoneInfo.ConvertTimeToUtc(from.AddDays(7), Moscow));
	}

	public static (DateTime From, DateTime To) Tomorrow(DateTime utcNow)
	{
		var local = TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(utcNow), Moscow);
		var from = local.Date.AddDays(1);
		return (TimeZoneInfo.ConvertTimeToUtc(from, Moscow), TimeZoneInfo.ConvertTimeToUtc(from.AddDays(1), Moscow));
	}

	public static DateTime MoscowDayStartUtc(DateTime utcNow)
	{
		var local = TimeZoneInfo.ConvertTimeFromUtc(EnsureUtc(utcNow), Moscow);
		return TimeZoneInfo.ConvertTimeToUtc(local.Date, Moscow);
	}

	private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc
		? value : throw new ArgumentException("Ожидается время UTC.", nameof(value));
}
