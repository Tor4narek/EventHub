using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Services.Interfaces;
using Storage;
using Storage.Entities;

namespace Services;

public sealed class BotUpdateInboxService : IBotUpdateInboxService
{
	private readonly AppDbContext _dbContext;

	public BotUpdateInboxService(AppDbContext dbContext) => _dbContext = dbContext;

	public async Task EnqueueAsync(string payload, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(payload);
		var now = DateTime.UtcNow;
		var update = new BotUpdate
		{
			Id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))),
			Payload = payload,
			ReceivedAt = now,
			NextAttemptAt = now
		};
		_dbContext.BotUpdates.Add(update);
		try
		{
			await _dbContext.SaveChangesAsync(cancellationToken);
		}
		catch (DbUpdateException ex) when (ex.InnerException is PostgresException
		       { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "PK_BotUpdates" })
		{
			_dbContext.Entry(update).State = EntityState.Detached;
		}
	}

	public async Task<IReadOnlyList<BotUpdate>> GetAvailableAsync(DateTime now, int batchSize, CancellationToken cancellationToken)
	{
		if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
		return await _dbContext.BotUpdates.AsNoTracking()
			.Where(u => u.ProcessedAt == null && u.NextAttemptAt <= now &&
				(u.LeaseUntil == null || u.LeaseUntil <= now))
			.OrderBy(u => u.ReceivedAt)
			.Take(batchSize)
			.ToListAsync(cancellationToken);
	}

	public async Task<bool> TryClaimAsync(string id, DateTime now, DateTime leaseUntil, CancellationToken cancellationToken) =>
		await _dbContext.BotUpdates
			.Where(u => u.Id == id && u.ProcessedAt == null && u.NextAttemptAt <= now &&
				(u.LeaseUntil == null || u.LeaseUntil <= now))
			.ExecuteUpdateAsync(setters => setters.SetProperty(u => u.LeaseUntil, leaseUntil)
				.SetProperty(u => u.AttemptCount, u => u.AttemptCount + 1), cancellationToken) == 1;

	public Task MarkProcessedAsync(string id, DateTime processedAt, CancellationToken cancellationToken) =>
		_dbContext.BotUpdates.Where(u => u.Id == id)
			.ExecuteUpdateAsync(setters => setters.SetProperty(u => u.ProcessedAt, processedAt)
				.SetProperty(u => u.LeaseUntil, (DateTime?)null), cancellationToken);

	public Task RecordFailureAsync(string id, DateTime nextAttemptAt, CancellationToken cancellationToken) =>
		_dbContext.BotUpdates.Where(u => u.Id == id && u.ProcessedAt == null)
			.ExecuteUpdateAsync(setters => setters.SetProperty(u => u.NextAttemptAt, nextAttemptAt)
				.SetProperty(u => u.LeaseUntil, (DateTime?)null), cancellationToken);

	public Task PurgeProcessedBeforeAsync(DateTime before, CancellationToken cancellationToken) =>
		_dbContext.BotUpdates.Where(u => u.ProcessedAt != null && u.ProcessedAt < before)
			.ExecuteDeleteAsync(cancellationToken);
}
