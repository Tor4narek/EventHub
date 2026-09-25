namespace Storage.Entities;

public class Event
{
	public Guid Id { get; set; }
	public string Title { get; set; }
	public string Description { get; set; }
	public DateTime EventDateTime { get; set; }
	public string Location { get; set; }
	public bool TagsConfirmed { get; set; } = true;
	public string Source { get; set; }
	public string? MainImg {get; set;}
	public EventStatus EventStatus { get; set; } = EventStatus.Draft;
	public DateTime? Deadline { get; set; }
	public long Views  { get; set; }
	public List<EventTag> Tags { get; set; } = [];
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
}
