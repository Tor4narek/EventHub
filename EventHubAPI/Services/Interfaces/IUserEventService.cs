using Storage.Entities;

namespace Services.Interfaces;

public interface IUserEventService
{
	Task SaveEventAsync(
		Guid userId,
		Guid eventId,
		CancellationToken cancellationToken);

	Task RemoveEventAsync(
		Guid userId,
		Guid eventId,
		CancellationToken cancellationToken);

	Task<bool> IsEventSavedAsync(
		Guid userId,
		Guid eventId,
		CancellationToken cancellationToken);

	Task<IReadOnlyList<Event>> GetSavedEventsAsync(
		Guid userId,
		CancellationToken cancellationToken);

	Task<IReadOnlyList<UserEvent>> GetDueRemindersAsync(
		DateTime from,
		DateTime to,
		int batchSize,
		CancellationToken cancellationToken);

	Task MarkReminderSentAsync(
		Guid userId,
		Guid eventId,
		DateTime sentAt,
		CancellationToken cancellationToken);

	Task<bool> TryClaimReminderAsync(
		Guid userId, Guid eventId, DateTime claimedAt, CancellationToken cancellationToken);

	Task ReleaseReminderClaimAsync(
		Guid userId, Guid eventId, DateTime claimedAt, CancellationToken cancellationToken);
}
