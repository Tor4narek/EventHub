namespace Storage.Entities;

public class UserTag
{
	public Guid TagId { get; set; }
	public Guid UserId { get; set; }
	public Tag Tag { get; set; } = null!;
	public User User { get; set; } = null!;
}