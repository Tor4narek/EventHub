using Storage.Entities;

namespace Services.Dto;

public record EventSearchFilter(
	int Page,
	int PageSize,
	IReadOnlyCollection<Guid> TagIds,
	string? Search = null,
	DateOnly? From = null,
	DateOnly? To = null,
	string? Format = null,
	EventStatus? Status = null,
	bool? TagsConfirmed = null
);
