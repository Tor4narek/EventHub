namespace Storage.Entities;

public class EventImportRun
{
	public Guid Id { get; set; }
	public DateTime CreatedAt { get; set; }
	public List<EventImportItem> Items { get; set; } = [];
}

public enum EventImportStatus { Pending, Processing, Ready, Failed, Confirmed, Duplicate }

public class EventImportItem
{
	public Guid Id { get; set; }
	public Guid ImportRunId { get; set; }
	public string SourceKey { get; set; } = "";
	public string Source { get; set; } = "";
	public EventImportStatus Status { get; set; }
	public string? Title { get; set; }
	public string? Description { get; set; }
	public string? OriginalDescription { get; set; }
	public DateTime? EventDateTime { get; set; }
	public DateTime? Deadline { get; set; }
	public string? Location { get; set; }
	public string? MainImg { get; set; }
	public Guid[] TagIds { get; set; } = [];
	public Guid[] SuggestedTagIds { get; set; } = [];
	public string? SuggestedDescription { get; set; }
	public string[] Warnings { get; set; } = [];
	public string? Error { get; set; }
	public Guid? EventId { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
	public DateTime? LeaseUntil { get; set; }
	public Guid? LeaseToken { get; set; }
}
