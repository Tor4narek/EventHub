namespace Services.Interfaces;

// Импортёр создаёт черновики с TagsConfirmed = false и возвращает id запуска.
public interface IEventImportService
{
	Task<Guid> StartImportAsync(CancellationToken cancellationToken);
}
