using Scheduler;
using Xunit;

namespace MaxBotCore.Tests.Scheduler;

public sealed class BotScheduleTests
{
	[Fact]
	public void Tomorrow_uses_moscow_date_at_utc_evening()
	{
		var (from, to) = BotSchedule.Tomorrow(new DateTime(2026, 9, 25, 22, 30, 0, DateTimeKind.Utc));
		Assert.Equal(new DateTime(2026, 9, 26, 21, 0, 0, DateTimeKind.Utc), from);
		Assert.Equal(new DateTime(2026, 9, 27, 21, 0, 0, DateTimeKind.Utc), to);
	}

	[Fact]
	public void Sunday_is_moscow_sunday_even_when_utc_is_saturday() =>
		Assert.True(BotSchedule.IsSunday(new DateTime(2026, 9, 26, 22, 0, 0, DateTimeKind.Utc)));

	[Fact]
	public void Next_week_starts_on_moscow_monday()
	{
		var (from, to) = BotSchedule.NextWeek(new DateTime(2026, 9, 26, 22, 0, 0, DateTimeKind.Utc));
		Assert.Equal(new DateTime(2026, 9, 27, 21, 0, 0, DateTimeKind.Utc), from);
		Assert.Equal(new DateTime(2026, 10, 4, 21, 0, 0, DateTimeKind.Utc), to);
	}
}
