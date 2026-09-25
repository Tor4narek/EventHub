using System.Text.Json.Serialization;

namespace MaxBotCore.Contracts.Common;

/// <summary>
/// Получатель сообщения. Для диалога с ботом chat_type = "dialog"
/// и user_id заполнен.
/// </summary>
public sealed class MaxRecipient
{
    [JsonPropertyName("chat_id")]
    public long? ChatId { get; init; }

    [JsonPropertyName("user_id")]
    public long? UserId { get; init; }

    [JsonPropertyName("chat_type")]
    public string? ChatType { get; init; }
}