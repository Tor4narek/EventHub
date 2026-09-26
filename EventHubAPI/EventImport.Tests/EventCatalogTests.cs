using Microsoft.EntityFrameworkCore;
using Services;
using Services.Dto;
using Storage.Entities;
using Xunit;

namespace EventImport.Tests;

public class EventCatalogTests(ImportDatabase database) : IClassFixture<ImportDatabase>
{
	[PostgresFact]
	public async Task Public_catalog_excludes_events_started_today_in_all_date_modes_but_admin_retains_them()
	{
		await using var db = database.CreateContext();
		var now = DateTime.UtcNow;
		var zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
		var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, zone));
		var midnight = TimeZoneInfo.ConvertTimeToUtc(today.ToDateTime(TimeOnly.MinValue), zone);
		var started = now.AddTicks(-(now - midnight).Ticks / 2);
		Event Create(string title, DateTime date, EventStatus status = EventStatus.Published) => new()
		{
			Id = Guid.NewGuid(), Title = title, Description = title, Location = "Онлайн", Source = "https://example.org/event",
			EventDateTime = date, EventStatus = status, CreatedAt = now, UpdatedAt = now
		};
		var past = Create("Started today", started);
		var soon = Create("Upcoming", now.AddHours(1));
		var far = Create("Later", now.AddMonths(2));
		var draft = Create("Draft", now.AddHours(2), EventStatus.Draft);
		db.Events.AddRange(past, soon, far, draft);
		await db.SaveChangesAsync();
		var service = new EventService(db);
		var all = await service.SearchPublishedEventsAsync(new EventSearchFilter(1, 1, [], AllDates: true), default);
		Assert.Equal(2, all.TotalCount); Assert.True(all.HasNextPage); Assert.Equal(soon.Id, Assert.Single(all.Items).Id);
		var second = await service.SearchPublishedEventsAsync(new EventSearchFilter(2, 1, [], AllDates: true), default);
		Assert.Equal(far.Id, Assert.Single(second.Items).Id); Assert.False(second.HasNextPage);
		var monthly = await service.SearchPublishedEventsAsync(new EventSearchFilter(1, 100, []), default);
		Assert.Equal(soon.Id, Assert.Single(monthly.Items).Id);
		var dates = await service.SearchPublishedEventsAsync(new EventSearchFilter(1, 100, [], From: today, To: today.AddDays(1)), default);
		Assert.Equal(soon.Id, Assert.Single(dates.Items).Id);
		var legacy = await service.GetEventsAsync(1, 100, [], default);
		Assert.Equal(2, legacy.TotalCount); Assert.DoesNotContain(legacy.Items, item => item.Id == past.Id || item.Id == draft.Id);
		var admin = await service.GetAdminEventsAsync(new EventSearchFilter(1, 100, []), default);
		Assert.Equal(4, admin.TotalCount); Assert.Contains(admin.Items, item => item.Id == past.Id);
	}
}
