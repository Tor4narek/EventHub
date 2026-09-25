namespace Services.Dto;

public record EventDto(
	string Title,
	string Description,
	DateTime EventDateTime,
	string Location,
	string Source,
	DateTime? Deadline,
	IReadOnlyCollection<Guid> TagIds,
	string? MainImg = null
);
