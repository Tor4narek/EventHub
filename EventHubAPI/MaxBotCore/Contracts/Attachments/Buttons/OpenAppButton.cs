using System.Text.Json.Serialization;

namespace MaxBotCore.Contracts.Attachments.Buttons;

/// <summary>Открывает подключённое к боту мини-приложение MAX.</summary>
public sealed class OpenAppButton : MaxButton
{
	[JsonPropertyName("web_app")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public string? WebApp { get; init; }

	[JsonPropertyName("contact_id")]
	[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
	public long? ContactId { get; init; }
}
