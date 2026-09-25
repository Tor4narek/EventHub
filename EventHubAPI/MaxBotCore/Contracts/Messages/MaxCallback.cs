using System.Text.Json.Serialization;
using MaxBotCore.Contracts.Common;

namespace MaxBotCore.Contracts.Messages;

/// <summary>
/// Данные, приходящие в message_callback Update, когда пользователь нажал inline-кнопку.
/// </summary>
public sealed class MaxCallback
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    [JsonPropertyName("callback_id")]
    public string? CallbackId { get; init; }

    [JsonPropertyName("payload")]
    public string? Payload { get; init; }

    [JsonPropertyName("user")]
    public MaxUser? User { get; init; }
}