using Microsoft.EntityFrameworkCore;
using Services.Interfaces;
using Storage;
using Storage.Entities;

namespace Services;

public class RecomendationService : IRecomendationService
{
	private const int SelectedTagScore = 3;
	private const int SavedEventTagScore = 1;
	private const int MaxSavedEventsPerTag = 3;

	private readonly AppDbContext _dbContext;

	public RecomendationService(AppDbContext dbContext)
	{
		ArgumentNullException.ThrowIfNull(dbContext);
		_dbContext = dbContext;
	}

	public async Task<IReadOnlyList<Event>> GetTopEventsAsync(
		Guid userId,
		int numberOfEvents,
		DateTime from,
		DateTime to,
		CancellationToken cancellationToken)
	{
		if (userId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(userId));
		}

		if (numberOfEvents <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(numberOfEvents));
		}

		if (to <= from)
		{
			throw new ArgumentException("Конец периода должен быть позже начала.");
		}

		var userExists = await _dbContext.Users
			.AnyAsync(u => u.Id == userId, cancellationToken);

		if (!userExists)
		{
			throw new KeyNotFoundException(
				$"Пользователь с Id {userId} не найден.");
		}

		var selectedTagIds = (await _dbContext.UserTags
			.Where(ut => ut.UserId == userId)
			.Select(ut => ut.TagId)
			.ToListAsync(cancellationToken))
			.ToHashSet();

		var savedEventIds = await _dbContext.UserEvents
			.Where(ue => ue.UserId == userId)
			.Select(ue => ue.EventId)
			.ToListAsync(cancellationToken);

		var savedTagIds = await (
			from userEvent in _dbContext.UserEvents
			join eventTag in _dbContext.EventTags
				on userEvent.EventId equals eventTag.EventId
			where userEvent.UserId == userId
			select eventTag.TagId
		).ToListAsync(cancellationToken);

		var savedTagCounts = savedTagIds
			.GroupBy(tagId => tagId)
			.ToDictionary(group => group.Key, group => group.Count());

		var now = DateTime.UtcNow;

		var query = _dbContext.Events
			.AsNoTracking()
			.Include(e => e.Tags)
			.Where(e =>
				e.EventStatus == EventStatus.Published &&
				e.EventDateTime >= from &&
				e.EventDateTime >= now &&
				e.EventDateTime < to &&
				(e.Deadline == null || e.Deadline >= now));

		if (savedEventIds.Count > 0)
		{
			query = query.Where(e => !savedEventIds.Contains(e.Id));
		}

		var events = await query.ToListAsync(cancellationToken);

		return events
			.OrderByDescending(e => e.Tags.Sum(tag =>
				(selectedTagIds.Contains(tag.TagId) ? SelectedTagScore : 0) +
				Math.Min(
					savedTagCounts.GetValueOrDefault(tag.TagId),
					MaxSavedEventsPerTag) * SavedEventTagScore))
			.ThenBy(e => e.EventDateTime)
			.ThenBy(e => e.Id)
			.Take(numberOfEvents)
			.ToList();
	}
}
