using Storage.Entities;

namespace Services.Interfaces;

public interface IUserService
{
	Task<User> CreateByMaxUserIdAsync(
		long maxUserId,
		CancellationToken cancellationToken);

	Task<User> GetByMaxUserIdAsync(
		long maxUserId,
		CancellationToken cancellationToken);

	Task<User?> GetByIdAsync(
		Guid userId,
		CancellationToken cancellationToken);

	Task<IReadOnlyList<Guid>> GetUserTagIdsAsync(
		Guid userId,
		CancellationToken cancellationToken);

	Task SetWeeklyDigestAsync(
		Guid userId,
		bool enabled,
		CancellationToken cancellationToken);

	Task UpdateUserTagsAsync(
		Guid userId,
		IReadOnlyCollection<Guid> tagIds,
		CancellationToken cancellationToken);

	Task<bool> CompleteOnboardingAsync(Guid userId, CancellationToken cancellationToken);

	Task<IReadOnlyList<User>> GetWeeklyDigestSubscribersAsync(
		DateTime weekStart, int batchSize, CancellationToken cancellationToken);

	Task<bool> TryClaimWeeklyDigestAsync(
		Guid userId, DateTime weekStart, DateTime claimedAt, CancellationToken cancellationToken);

	Task ReleaseWeeklyDigestClaimAsync(
		Guid userId, DateTime claimedAt, CancellationToken cancellationToken);
}
