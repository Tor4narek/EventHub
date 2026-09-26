using System.Text.Json.Serialization;

namespace MaxBotCore.Contracts.Attachments.Buttons;

/// <summary>Открывает подключённое к боту мини-приложение MAX.</summary>
public sealed class OpenAppButton : MaxButton
{
	[JsonPropertyName("web_app")]
	public required string WebApp { get; init; }
}
