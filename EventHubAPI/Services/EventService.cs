using Microsoft.EntityFrameworkCore;
using Services.Dto;
using Services.Interfaces;
using Storage;
using Storage.Entities;

namespace Services;

public class EventService : IEventService
{
	private readonly AppDbContext _dbContext;

	public EventService(AppDbContext dbContext)
	{
		ArgumentNullException.ThrowIfNull(dbContext);
		_dbContext = dbContext;
	}

	public async Task<Event> CreateEventAsync(EventDto eventDto, CancellationToken cancellationToken)
	{
		var tagIds = await ValidateEventDtoAsync(eventDto, cancellationToken);
		var now = DateTime.UtcNow;
		var newEvent = new Event
		{
			Id = Guid.NewGuid(),
			Title = eventDto.Title,
			Description = eventDto.Description,
			EventDateTime = eventDto.EventDateTime,
			Location = eventDto.Location,
			Source = eventDto.Source,
			MainImg = eventDto.MainImg,
			Deadline = eventDto.Deadline,
			CreatedAt = now,
			UpdatedAt = now,
			Tags = tagIds.Select(tagId => new EventTag
			{
				TagId = tagId
			}).ToList()
		};

		_dbContext.Events.Add(newEvent);
		await _dbContext.SaveChangesAsync(cancellationToken);

		return newEvent;
	}

	public async Task<Event> GetEventByIdAsync(Guid eventId, CancellationToken cancellationToken, EventStatus? eventStatus = null)
	{
		if (eventId == Guid.Empty)
		{
			throw new ArgumentException($"Передан пустой id", nameof(eventId));
		}

		return await _dbContext.Events.AsNoTracking().Include(e => e.Tags)
			.FirstOrDefaultAsync(e => e.Id == eventId && (eventStatus == null || e.EventStatus == eventStatus), cancellationToken)
		       ?? throw new KeyNotFoundException($"Мероприятия с Id {eventId} не существует.");
	}

	public async Task<PagedResult<Event>> GetEventsAsync(int page, int pageSize, IReadOnlyCollection<Guid> tags, CancellationToken cancellationToken)
	{
		if (page < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(page));
		}

		if (pageSize < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(pageSize));
		}

		ArgumentNullException.ThrowIfNull(tags);

		var tagIds = tags.ToHashSet();
		var now = DateTime.UtcNow;

		var query = _dbContext.Events
			.AsNoTracking()
			.Where(e => e.EventStatus == EventStatus.Published && e.EventDateTime > now);

		if (tagIds.Count > 0)
		{
			query = query.Where(e =>
				e.Tags.Any(t => tagIds.Contains(t.TagId)));
		}

		var totalCount = await query.CountAsync(cancellationToken);

		var events = await query
			.OrderBy(e => e.EventDateTime)
			.ThenBy(e => e.Id)
			.Skip((page - 1) * pageSize)
			.Take(pageSize)
			.ToListAsync(cancellationToken);

		return new PagedResult<Event>(
			events,
			page,
			pageSize,
			totalCount,
			totalCount > (long)page * pageSize
		);
	}

	public Task<PagedResult<Event>> SearchPublishedEventsAsync(EventSearchFilter filter, CancellationToken cancellationToken)
	{
		return SearchEventsAsync(filter, true, cancellationToken);
	}

	public Task<PagedResult<Event>> GetAdminEventsAsync(EventSearchFilter filter, CancellationToken cancellationToken)
	{
		return SearchEventsAsync(filter, false, cancellationToken);
	}

	private async Task<PagedResult<Event>> SearchEventsAsync(EventSearchFilter filter, bool publishedOnly, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(filter);
		ArgumentNullException.ThrowIfNull(filter.TagIds);
		if (filter.Page < 1 || filter.PageSize is < 1 or > 100)
		{
			throw new ArgumentOutOfRangeException(nameof(filter));
		}

		var now = DateTime.UtcNow;
		var moscowTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow");
		var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(now, moscowTimeZone));
		var from = publishedOnly && (filter.From is null || filter.From < today) ? today : filter.From;
		var to = publishedOnly && !filter.AllDates
			? filter.To ?? (from ?? today).AddMonths(1).AddDays(-1)
			: filter.To;
		if (from is not null && to is not null && to < from)
		{
			throw new ArgumentException("Конец периода должен быть не раньше начала.");
		}

		var query = _dbContext.Events.AsNoTracking().AsQueryable();
		if (publishedOnly)
		{
			query = query.Where(e => e.EventStatus == EventStatus.Published && e.EventDateTime > now);
		}
		else if (filter.Status is not null)
		{
			query = query.Where(e => e.EventStatus == filter.Status);
		}
		if (!publishedOnly && filter.TagsConfirmed is not null)
		{
			query = query.Where(e => e.TagsConfirmed == filter.TagsConfirmed);
		}

		if (from is not null)
		{
			var fromUtc = TimeZoneInfo.ConvertTimeToUtc(from.Value.ToDateTime(TimeOnly.MinValue), moscowTimeZone);
			query = query.Where(e => e.EventDateTime >= fromUtc);
		}

		if (to is not null)
		{
			var toUtc = TimeZoneInfo.ConvertTimeToUtc(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), moscowTimeZone);
			query = query.Where(e => e.EventDateTime < toUtc);
		}

		if (!string.IsNullOrWhiteSpace(filter.Format))
		{
			switch (filter.Format.Trim().ToLowerInvariant())
			{
				case "online":
					query = query.Where(e => EF.Functions.ILike(e.Location.Trim(), "онлайн") ||
					                         EF.Functions.ILike(e.Location.Trim(), "online"));
					break;
				case "offline":
					query = query.Where(e => !EF.Functions.ILike(e.Location.Trim(), "онлайн") &&
					                         !EF.Functions.ILike(e.Location.Trim(), "online"));
					break;
				default:
					throw new ArgumentException("format должен быть online или offline.", nameof(filter));
			}
		}

		var tagIds = filter.TagIds.ToHashSet();
		if (tagIds.Count > 0)
		{
			query = query.Where(e => e.Tags.Any(t => tagIds.Contains(t.TagId)));
		}

		var search = filter.Search?.Trim();
		if (!string.IsNullOrEmpty(search))
		{
			var pattern = $"%{search.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
			query = query.Where(e => EF.Functions.ILike(e.Title, pattern, "\\") ||
			                         EF.Functions.ILike(e.Description, pattern, "\\"));
		}

		var totalCount = await query.CountAsync(cancellationToken);
		var events = await query
			.Include(e => e.Tags)
			.OrderBy(e => e.EventDateTime)
			.ThenBy(e => e.Id)
			.Skip((filter.Page - 1) * filter.PageSize)
			.Take(filter.PageSize)
			.ToListAsync(cancellationToken);

		return new PagedResult<Event>(events, filter.Page, filter.PageSize, totalCount,
			totalCount > (long)filter.Page * filter.PageSize);
	}

	public async Task<Event> ConfirmEventTagsAsync(Guid eventId, IReadOnlyCollection<Guid> tagIds, CancellationToken cancellationToken)
	{
		if (eventId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(eventId));
		}

		ArgumentNullException.ThrowIfNull(tagIds);
		var requestedIds = tagIds.ToHashSet();
		var existingTagCount = await _dbContext.Tags.CountAsync(t => requestedIds.Contains(t.Id), cancellationToken);
		if (existingTagCount != requestedIds.Count)
		{
			throw new ArgumentException("Один или несколько тегов не существуют.", nameof(tagIds));
		}

		var existingEvent = await _dbContext.Events.Include(e => e.Tags)
			.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken)
			?? throw new KeyNotFoundException($"Мероприятия с Id {eventId} не существует.");

		var currentIds = existingEvent.Tags.Select(t => t.TagId).ToHashSet();
		_dbContext.EventTags.RemoveRange(existingEvent.Tags.Where(t => !requestedIds.Contains(t.TagId)));
		_dbContext.EventTags.AddRange(requestedIds.Except(currentIds)
			.Select(tagId => new EventTag { EventId = eventId, TagId = tagId }));
		existingEvent.UpdatedAt = DateTime.UtcNow;
		existingEvent.TagsConfirmed = true;
		await _dbContext.SaveChangesAsync(cancellationToken);
		return existingEvent;
	}

	public async Task<Event> UpdateEventAsync(Guid eventId, EventDto eventDto, CancellationToken cancellationToken)
	{
		if (eventId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(eventId));
		}

		var tagIds = await ValidateEventDtoAsync(eventDto, cancellationToken);

		var existingEvent = await _dbContext.Events
			                    .Include(e => e.Tags)
			                    .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken)
		                    ?? throw new KeyNotFoundException($"Мероприятия с Id {eventId} не существует.");

		existingEvent.Title = eventDto.Title;
		existingEvent.Description = eventDto.Description;
		existingEvent.EventDateTime = eventDto.EventDateTime;
		existingEvent.Location = eventDto.Location;
		existingEvent.Source = eventDto.Source;
		existingEvent.MainImg = eventDto.MainImg;
		existingEvent.Deadline = eventDto.Deadline;
		existingEvent.UpdatedAt = DateTime.UtcNow;

		var currentTagIds = existingEvent.Tags
			.Select(t => t.TagId)
			.ToHashSet();

		_dbContext.EventTags.RemoveRange(
			existingEvent.Tags.Where(t => !tagIds.Contains(t.TagId)));

		_dbContext.EventTags.AddRange(
			tagIds.Except(currentTagIds)
				.Select(tagId => new EventTag
				{
					EventId = eventId,
					TagId = tagId
				}));

		await _dbContext.SaveChangesAsync(cancellationToken);

		return existingEvent;
	}

	public async Task<Event> ChangeEventStatusAsync(Guid eventId, EventStatus eventStatus, CancellationToken cancellationToken)
	{
		if (eventId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(eventId));
		}

		var existingEvent = await _dbContext.Events.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken)
		                    ?? throw new KeyNotFoundException($"Мероприятия с Id {eventId} не существует.");

		if (eventStatus == EventStatus.Published && !existingEvent.TagsConfirmed)
		{
			throw new InvalidOperationException("Перед публикацией нужно подтвердить теги мероприятия.");
		}

		existingEvent.EventStatus = eventStatus;
		existingEvent.UpdatedAt = DateTime.UtcNow;

		await _dbContext.SaveChangesAsync(cancellationToken);

		return existingEvent;
	}

	public async Task UnpublishEventAsync(Guid eventId, CancellationToken cancellationToken)
	{
		await ChangeEventStatusAsync(eventId, EventStatus.Draft, cancellationToken);
	}

	private async Task<HashSet<Guid>> ValidateEventDtoAsync(EventDto eventDto, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(eventDto);
		ArgumentException.ThrowIfNullOrWhiteSpace(eventDto.Title);
		ArgumentException.ThrowIfNullOrWhiteSpace(eventDto.Description);
		ArgumentException.ThrowIfNullOrWhiteSpace(eventDto.Location);
		ArgumentException.ThrowIfNullOrWhiteSpace(eventDto.Source);
		ArgumentNullException.ThrowIfNull(eventDto.TagIds);

		var now = DateTime.UtcNow;

		if (eventDto.EventDateTime <= now)
		{
			throw new ArgumentException("Дата мероприятия должна быть в будущем.");
		}

		if (eventDto.Deadline > eventDto.EventDateTime)
		{
			throw new ArgumentException("Дедлайн регистрации не может быть позже мероприятия.");
		}

		var tagIds = eventDto.TagIds.ToHashSet();

		var existingTagCount = await _dbContext.Tags
			.CountAsync(t => tagIds.Contains(t.Id), cancellationToken);

		if (existingTagCount != tagIds.Count)
		{
			throw new ArgumentException("Один или несколько тегов не существуют.");
		}

		return tagIds;
	}
}
