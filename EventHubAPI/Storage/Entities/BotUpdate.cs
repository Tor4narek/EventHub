namespace Storage.Entities;

public class BotUpdate
{
	public string Id { get; set; } = string.Empty;
	public string Payload { get; set; } = string.Empty;
	public DateTime ReceivedAt { get; set; }
	public DateTime? ProcessedAt { get; set; }
	public DateTime? LeaseUntil { get; set; }
	public DateTime NextAttemptAt { get; set; }
	public int AttemptCount { get; set; }
}
