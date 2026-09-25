namespace Storage.Entities;

public class Tag
{
	public Guid Id { get; set; }
	public string Name { get; set; }
	public string Description { get; set; }
	public List<string> Examples { get; set; }
	public List<UserTag> UserTags { get; } = [];
	public List<EventTag> EventTags { get; } = [];
}