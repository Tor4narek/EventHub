using Services.Dto;
using Storage.Entities;

namespace Services.Interfaces;

public interface IEventImportService
{
	Task<Guid> StartImportAsync(IReadOnlyCollection<string> urls, CancellationToken cancellationToken);
	Task<IReadOnlyCollection<EventImportRun>> GetImportsAsync(CancellationToken cancellationToken);
	Task<EventImportRun> GetImportAsync(Guid importId, CancellationToken cancellationToken);
	Task<EventImportItem> GetItemAsync(Guid importId, Guid itemId, CancellationToken cancellationToken);
	Task<EventImportItem> UpdateItemAsync(Guid importId, Guid itemId, EventImportEditDto draft, CancellationToken cancellationToken);
	Task<Guid> ConfirmItemAsync(Guid importId, Guid itemId, CancellationToken cancellationToken);
	Task RetryItemAsync(Guid importId, Guid itemId, CancellationToken cancellationToken);
}
