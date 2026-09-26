using Microsoft.EntityFrameworkCore;
using Services.Interfaces;
using Npgsql;
using Storage;
using Storage.Entities;

namespace Services;

public class UserEventService : IUserEventService
{
	private readonly AppDbContext _dbContext;

	public UserEventService(AppDbContext dbContext)
	{
		ArgumentNullException.ThrowIfNull(dbContext);
		_dbContext = dbContext;
	}

	public async Task SaveEventAsync(
		Guid userId,
		Guid eventId,
		CancellationToken cancellationToken)
	{
		if (userId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(userId));
		}

		if (eventId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(eventId));
		}

		var userExists = await _dbContext.Users
			.AnyAsync(u => u.Id == userId, cancellationToken);

		if (!userExists)
		{
			throw new KeyNotFoundException($"Пользователь с Id {userId} не найден.");
		}

		var eventToSave = await _dbContext.Events
			.AsNoTracking()
			.FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken)
			?? throw new KeyNotFoundException(
				$"Мероприятия с Id {eventId} не существует.");

		if (eventToSave.EventStatus != EventStatus.Published ||
		    eventToSave.EventDateTime <= DateTime.UtcNow)
		{
			throw new InvalidOperationException(
				"Можно сохранить только опубликованное предстоящее мероприятие.");
		}

		var alreadySaved = await _dbContext.UserEvents
			.AnyAsync(ue =>
				ue.UserId == userId && ue.EventId == eventId,
				cancellationToken);

		if (alreadySaved)
		{
			return;
		}

		var userEvent = new UserEvent
		{
			UserId = userId,
			EventId = eventId,
			CreateAt = DateTime.UtcNow
		};

		_dbContext.UserEvents.Add(userEvent);
		try
		{
			await _dbContext.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException ex) when (ex.InnerException is PostgresException
		       {
			       SqlState: PostgresErrorCodes.UniqueViolation,
			       ConstraintName: "PK_UserEvents"
		       })
		{
			_dbContext.Entry(userEvent).State = EntityState.Detached;
		}
	}

	public async Task RemoveEventAsync(
		Guid userId,
		Guid eventId,
		CancellationToken cancellationToken)
	{
		if (userId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(userId));
		}

		if (eventId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(eventId));
		}

		var userEvent = await _dbContext.UserEvents
			.FirstOrDefaultAsync(ue =>
				ue.UserId == userId && ue.EventId == eventId,
				cancellationToken);

		if (userEvent is null)
		{
			return;
		}

		_dbContext.UserEvents.Remove(userEvent);
		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	public Task<bool> IsEventSavedAsync(
		Guid userId,
		Guid eventId,
		CancellationToken cancellationToken)
	{
		if (userId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(userId));
		}

		if (eventId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(eventId));
		}

		return _dbContext.UserEvents
			.AnyAsync(ue =>
				ue.UserId == userId && ue.EventId == eventId,
				cancellationToken);
	}

	public async Task<IReadOnlyList<Event>> GetSavedEventsAsync(
		Guid userId,
		CancellationToken cancellationToken)
	{
		if (userId == Guid.Empty)
		{
			throw new ArgumentException("Передан пустой id", nameof(userId));
		}

		return await _dbContext.Events
			.AsNoTracking()
			.Where(e => e.EventStatus == EventStatus.Published &&
				_dbContext.UserEvents.Any(ue => ue.UserId == userId && ue.EventId == e.Id))
			.Include(e => e.Tags)
			.OrderBy(e => e.EventDateTime)
			.ToListAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<UserEvent>> GetDueRemindersAsync(
		DateTime from,
		DateTime to,
		int batchSize,
		CancellationToken cancellationToken)
	{
		if (to <= from)
		{
			throw new ArgumentException("Конец периода должен быть позже начала.");
		}

		if (batchSize <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(batchSize));
		}

		return await _dbContext.UserEvents
			.AsNoTracking()
			.Include(ue => ue.User)
			.Include(ue => ue.Event)
			.Where(ue =>
				ue.ReminderSentAt == null &&
				ue.Event.EventStatus == EventStatus.Published &&
				ue.Event.EventDateTime >= from &&
				ue.Event.EventDateTime < to)
			.OrderBy(ue => ue.Event.EventDateTime)
			.ThenBy(ue => ue.UserId)
			.Take(batchSize)
			.ToListAsync(cancellationToken);
	}

	public async Task MarkReminderSentAsync(
		Guid userId,
		Guid eventId,
		DateTime sentAt,
		CancellationToken cancellationToken)
	{
		var userEvent = await _dbContext.UserEvents
			.FirstOrDefaultAsync(ue =>
				ue.UserId == userId && ue.EventId == eventId,
				cancellationToken)
			?? throw new KeyNotFoundException(
				"Сохранённое мероприятие не найдено.");

		userEvent.ReminderSentAt = sentAt;
		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task<bool> TryClaimReminderAsync(
		Guid userId, Guid eventId, DateTime claimedAt, CancellationToken cancellationToken) =>
		await _dbContext.UserEvents
			.Where(ue => ue.UserId == userId && ue.EventId == eventId && ue.ReminderSentAt == null &&
				ue.Event.EventStatus == EventStatus.Published)
			.ExecuteUpdateAsync(setters => setters.SetProperty(ue => ue.ReminderSentAt, claimedAt), cancellationToken) == 1;

	public Task ReleaseReminderClaimAsync(
		Guid userId, Guid eventId, DateTime claimedAt, CancellationToken cancellationToken) =>
		_dbContext.UserEvents
			.Where(ue => ue.UserId == userId && ue.EventId == eventId && ue.ReminderSentAt == claimedAt)
			.ExecuteUpdateAsync(setters => setters.SetProperty(ue => ue.ReminderSentAt, (DateTime?)null), cancellationToken);
}
