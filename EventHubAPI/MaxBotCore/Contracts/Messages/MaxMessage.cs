using System.Text.Json.Serialization;
using MaxBotCore.Contracts.Common;

namespace MaxBotCore.Contracts.Messages;

/// <summary>
/// Сообщение MAX. Приходит в теле Update при message_created,
/// а также возвращается в ответ на POST /messages.
/// </summary>
public sealed class MaxMessage
{
    [JsonPropertyName("sender")]
    public MaxUser? Sender { get; init; }

    [JsonPropertyName("recipient")]
    public MaxRecipient? Recipient { get; init; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    [JsonPropertyName("body")]
    public MaxMessageBody? Body { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}