using Services.Dto;
using Storage.Entities;

namespace EventHubAPI.Models;

public record EventResponse(
	Guid Id,
	string Title,
	string Description,
	DateTime EventDateTime,
	string Location,
	string Source,
	string? MainImg,
	DateTime? Deadline,
	EventStatus EventStatus,
	bool TagsConfirmed,
	IReadOnlyList<Guid> TagIds,
	DateTime CreatedAt,
	DateTime UpdatedAt)
{
	public static EventResponse From(Event item) => new(
		item.Id, item.Title, item.Description, item.EventDateTime,
		item.Location, item.Source, item.MainImg, item.Deadline,
		item.EventStatus, item.TagsConfirmed,
		item.Tags.Select(t => t.TagId).ToList(), item.CreatedAt, item.UpdatedAt);
}

public record TagResponse(Guid Id, string Name, string Description, IReadOnlyList<string> Examples)
{
	public static TagResponse From(Tag tag) => new(tag.Id, tag.Name, tag.Description, tag.Examples);
}

public record EventSearchRequest
{
	public int Page { get; init; } = 1;
	public int PageSize { get; init; } = 20;
	public Guid[] Tags { get; init; } = [];
	public string? Search { get; init; }
	public DateOnly? From { get; init; }
	public DateOnly? To { get; init; }
	public string? Format { get; init; }
	public EventStatus? Status { get; init; }
	public bool? TagsConfirmed { get; init; }
	public bool AllDates { get; init; }

	public EventSearchFilter ToFilter() => new(
		Page, PageSize, Tags, Search, From, To, Format, Status, TagsConfirmed, AllDates);
}

public record MaxLoginRequest(string InitData);
public record AdminLoginRequest(string Username, string Password);
public record TokenResponse(string AccessToken, DateTime ExpiresAt);
public record TagIdsRequest(IReadOnlyCollection<Guid> TagIds);
public record SettingsRequest(bool IsWeeklyDigestEnabled);
public record TagRequest(string Name, string Description, IReadOnlyCollection<string> Examples);
