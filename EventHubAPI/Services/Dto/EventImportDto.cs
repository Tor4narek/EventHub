using System.ComponentModel.DataAnnotations;

namespace Services.Dto;

public record EventImportRequest([Required, MinLength(1), MaxLength(50)] string[] Urls);

public record EventImportEditDto(
	[StringLength(300)] string? Title,
	[StringLength(100000)] string? Description,
	DateTime? EventDateTime,
	[StringLength(300)] string? Location,
	DateTime? Deadline,
	[Required, MaxLength(200)] Guid[] TagIds,
	[StringLength(2048)] string? MainImg = null);
