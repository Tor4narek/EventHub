using System.Text.Json.Serialization;
using MaxBotCore.Contracts.Attachments;

namespace MaxBotCore.Contracts.Messages;

/// <summary>
/// Тело сообщения: текст и/или вложения.
/// </summary>
public sealed class MaxMessageBody
{
    [JsonPropertyName("mid")]
    public string? Mid { get; init; }

    [JsonPropertyName("seq")]
    public long? Seq { get; init; }

    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("attachments")]
    public IReadOnlyList<MaxAttachment>? Attachments { get; init; }
}