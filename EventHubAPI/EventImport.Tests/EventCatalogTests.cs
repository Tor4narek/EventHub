using Microsoft.EntityFrameworkCore;
using Services;
using Services.Dto;
using Storage.Entities;
using Xunit;

namespace EventImport.Tests;

public class EventCatalogTests(ImportDatabase database) : IClassFixture<ImportDatabase>
{
	[PostgresFact]
	public async Task Only_draft_can_be_deleted_with_its_saves_tags_and_import_links()
	{
		await using var db = database.CreateContext();
		var now = DateTime.UtcNow;
		var draft = new Event
		{
			Id = Guid.NewGuid(), Title = "Draft to delete", Description = "Description", Location = "Online",
			Source = "https://example.org/event", EventDateTime = now.AddDays(1), CreatedAt = now, UpdatedAt = now
		};
		var published = new Event
		{
			Id = Guid.NewGuid(), Title = "Published to keep", Description = "Description", Location = "Online",
			Source = "https://example.org/other", EventDateTime = now.AddDays(1),
			EventStatus = EventStatus.Published, CreatedAt = now, UpdatedAt = now
		};
		var tag = new Tag { Id = Guid.NewGuid(), Name = "Delete test " + Guid.NewGuid(), Description = "Test", Examples = [] };
		var user = new User { Id = Guid.NewGuid(), MaxUserId = Random.Shared.NextInt64(1, long.MaxValue) };
		var run = new EventImportRun { Id = Guid.NewGuid(), CreatedAt = now };
		var import = new EventImportItem
		{
			Id = Guid.NewGuid(), ImportRunId = run.Id, EventId = draft.Id, SourceKey = Guid.NewGuid().ToString("N"),
			Source = draft.Source, Status = EventImportStatus.Confirmed, CreatedAt = now, UpdatedAt = now
		};
		db.AddRange(draft, published, tag, user, run);
		db.EventTags.Add(new EventTag { EventId = draft.Id, TagId = tag.Id });
		db.UserEvents.Add(new UserEvent { EventId = draft.Id, UserId = user.Id });
		db.EventImportItems.Add(import);
		await db.SaveChangesAsync();

		var service = new EventService(db);
		await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteDraftEventAsync(published.Id, default));
		Assert.True(await db.Events.AnyAsync(e => e.Id == published.Id));
		await service.DeleteDraftEventAsync(draft.Id, default);
		Assert.False(await db.Events.AnyAsync(e => e.Id == draft.Id));
		Assert.False(await db.EventTags.AnyAsync(e => e.EventId == draft.Id));
		Assert.False(await db.UserEvents.AnyAsync(e => e.EventId == draft.Id));
		Assert.False(await db.EventImportItems.AnyAsync(e => e.Id == import.Id));
		Assert.True(await db.EventImportRuns.AnyAsync(e => e.Id == run.Id));
		Assert.True(await db.Tags.AnyAsync(e => e.Id == tag.Id));
		Assert.True(await db.Users.AnyAsync(e => e.Id == user.Id));
	}

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
