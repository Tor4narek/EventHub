using Microsoft.EntityFrameworkCore;
using Services.Interfaces;
using Npgsql;
using Storage;
using Storage.Entities;

namespace Services;

public class UserService : IUserService
{
	private readonly AppDbContext _dbContext;

	public UserService(AppDbContext dbContext)
	{
		ArgumentNullException.ThrowIfNull(dbContext);
		_dbContext =  dbContext;
	}

	public async Task<User> CreateByMaxUserIdAsync(long maxUserId, CancellationToken cancellationToken)
	{
        ArgumentOutOfRangeException.ThrowIfNegative(maxUserId);
        var user = await _dbContext.Users
			.AsNoTracking()
			.FirstOrDefaultAsync(u =>u.MaxUserId == maxUserId,  cancellationToken);

        if (user != null)
        {
	        return user;
        }

        user = new User
        {
	        Id = Guid.NewGuid(),
	        MaxUserId = maxUserId,
	        IsWeeklyDigestEnabled = true,
        };

        _dbContext.Users.Add(user);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
               {
                   SqlState: PostgresErrorCodes.UniqueViolation,
                   ConstraintName: "IX_Users_MaxUserId"
               })
        {
            _dbContext.Entry(user).State = EntityState.Detached;
            return await _dbContext.Users
                .AsNoTracking()
                .FirstAsync(u => u.MaxUserId == maxUserId, cancellationToken);
        }

        return user;
	}

	public async Task<User> GetByMaxUserIdAsync(long maxUserId, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(maxUserId);
		var user = await _dbContext.Users
			.AsNoTracking()
			.FirstOrDefaultAsync(u =>u.MaxUserId == maxUserId,  cancellationToken);

		return user ?? throw new KeyNotFoundException($"Пользователь с maxID {maxUserId} не найден.");
	}

	public async Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken)
	{
		if (userId == Guid.Empty)
		{
			throw new ArgumentException($"Передан пустой id",nameof(userId));
		}

		var user = await _dbContext.Users
			.AsNoTracking()
			.FirstOrDefaultAsync(u =>u.Id == userId,  cancellationToken);

		return user ?? throw new KeyNotFoundException($"Пользователь с Id {userId} не найден.");
	}

	public async Task SetWeeklyDigestAsync(Guid userId, bool enabled, CancellationToken cancellationToken)
	{
		if (userId == Guid.Empty)
		{
			throw new ArgumentException($"Передан пустой id",nameof(userId));
		}

		var user = await _dbContext.Users
			           .FirstOrDefaultAsync(u =>u.Id == userId,  cancellationToken)
		           ?? throw new KeyNotFoundException($"Пользователь с Id {userId} не найден.");

		user.IsWeeklyDigestEnabled = enabled;
		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<Guid>> GetUserTagIdsAsync(Guid userId, CancellationToken cancellationToken)
	{
		if (userId == Guid.Empty)
		{
			throw new ArgumentException($"Передан пустой id",nameof(userId));
		}

		return await _dbContext.UserTags.AsNoTracking()
			.Where(ut => ut.UserId == userId)
			.Select(ut => ut.TagId)
			.ToListAsync(cancellationToken);
	}

	public async Task UpdateUserTagsAsync(Guid userId, IReadOnlyCollection<Guid> tagIds, CancellationToken cancellationToken)
	{
		if (userId == Guid.Empty)
		{
			throw new ArgumentException($"Передан пустой id",nameof(userId));
		}

		ArgumentNullException.ThrowIfNull(tagIds);

		var userExists = await _dbContext.Users
			.AnyAsync(u => u.Id == userId, cancellationToken);

		if (!userExists)
		{
			throw new KeyNotFoundException($"Пользователь с Id {userId} не найден.");
		}

		var requestedIds=  tagIds.ToHashSet();
		var existingTagIds = await _dbContext.Tags
			.Where(t => requestedIds.Contains(t.Id))
			.Select(t => t.Id)
			.ToListAsync(cancellationToken);

		if (existingTagIds.Count != requestedIds.Count)
		{
			throw new ArgumentException("Один или несколько тегов не существуют.", nameof(tagIds));
		}

		var currentUserTags = await _dbContext.UserTags
			.Where(ut => ut.UserId == userId)
			.ToListAsync(cancellationToken);

		var currentIds = currentUserTags.Select(ut => ut.TagId).ToHashSet();

		_dbContext.UserTags.RemoveRange(
			currentUserTags.Where(ut => !requestedIds.Contains(ut.TagId)));

		_dbContext.UserTags.AddRange(
			requestedIds.Except(currentIds)
				.Select(tagId => new UserTag { UserId = userId, TagId = tagId }));

		await _dbContext.SaveChangesAsync(cancellationToken);
	}

	public async Task<bool> CompleteOnboardingAsync(Guid userId, CancellationToken cancellationToken)
	{
		if (userId == Guid.Empty) throw new ArgumentException("Передан пустой id", nameof(userId));
		return await _dbContext.Users
			.Where(u => u.Id == userId && !u.HasCompletedOnboarding && u.UserTags.Any())
			.ExecuteUpdateAsync(setters => setters.SetProperty(u => u.HasCompletedOnboarding, true), cancellationToken) == 1;
	}

	public async Task<IReadOnlyList<User>> GetWeeklyDigestSubscribersAsync(
		DateTime weekStart, int batchSize, CancellationToken cancellationToken)
	{
		if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
		return await _dbContext.Users.AsNoTracking()
			.Where(u => u.IsWeeklyDigestEnabled && u.HasCompletedOnboarding && u.UserTags.Any() &&
				(u.LastWeeklyDigestAt == null || u.LastWeeklyDigestAt < weekStart))
			.OrderBy(u => u.Id)
			.Take(batchSize)
			.ToListAsync(cancellationToken);
	}

	public async Task<bool> TryClaimWeeklyDigestAsync(
		Guid userId, DateTime weekStart, DateTime claimedAt, CancellationToken cancellationToken) =>
		await _dbContext.Users
			.Where(u => u.Id == userId && u.IsWeeklyDigestEnabled &&
				(u.LastWeeklyDigestAt == null || u.LastWeeklyDigestAt < weekStart))
			.ExecuteUpdateAsync(setters => setters.SetProperty(u => u.LastWeeklyDigestAt, claimedAt), cancellationToken) == 1;

	public Task ReleaseWeeklyDigestClaimAsync(
		Guid userId, DateTime claimedAt, CancellationToken cancellationToken) =>
		_dbContext.Users
			.Where(u => u.Id == userId && u.LastWeeklyDigestAt == claimedAt)
			.ExecuteUpdateAsync(setters => setters.SetProperty(u => u.LastWeeklyDigestAt, (DateTime?)null), cancellationToken);
}
