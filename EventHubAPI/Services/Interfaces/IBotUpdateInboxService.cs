using Storage.Entities;

namespace Services.Interfaces;

public interface IBotUpdateInboxService
{
	Task EnqueueAsync(string payload, CancellationToken cancellationToken);
	Task<IReadOnlyList<BotUpdate>> GetAvailableAsync(DateTime now, int batchSize, CancellationToken cancellationToken);
	Task<bool> TryClaimAsync(string id, DateTime now, DateTime leaseUntil, CancellationToken cancellationToken);
	Task MarkProcessedAsync(string id, DateTime processedAt, CancellationToken cancellationToken);
	Task RecordFailureAsync(string id, DateTime nextAttemptAt, CancellationToken cancellationToken);
	Task PurgeProcessedBeforeAsync(DateTime before, CancellationToken cancellationToken);
}
