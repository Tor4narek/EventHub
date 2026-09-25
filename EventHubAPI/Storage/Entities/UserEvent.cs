namespace Storage.Entities;

public class UserEvent
{
	public Guid EventId { get; set; }
	public Guid UserId { get; set; }
	public Event Event { get; set; } = null!;
	public User User { get; set; } = null!;
	public DateTime CreateAt { get; set; } = DateTime.UtcNow;
	public DateTime? ReminderSentAt { get; set; }
}