namespace Storage.Entities;

public class User
{
	public Guid Id { get; set; }
	public long MaxUserId { get; set; }
	public bool IsWeeklyDigestEnabled { get; set; }
	public bool HasCompletedOnboarding { get; set; }
	public DateTime? LastWeeklyDigestAt { get; set; }
	public List<UserTag> UserTags { get; set; } = [];
}
