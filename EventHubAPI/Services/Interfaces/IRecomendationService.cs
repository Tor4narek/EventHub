using Storage.Entities;

namespace Services.Interfaces;

public interface IRecomendationService
{
	Task<IReadOnlyList<Event>> GetTopEventsAsync(
		Guid userId,
		int numberOfEvents,
		DateTime from,
		DateTime to,
		CancellationToken cancellationToken);
}