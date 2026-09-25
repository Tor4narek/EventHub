using System.Text.Json.Serialization;
using MaxBotCore.Contracts.Common;

namespace MaxBotCore.Contracts.Updates;

/// <summary>
/// Бот добавлен в групповой чат или канал. Для нашего сценария (личный диалог)
/// это событие не используется, но тип нужен для корректной десериализации.
/// </summary>
public sealed class BotAddedUpdate : MaxUpdate
{
    [JsonPropertyName("chat_id")]
    public long ChatId { get; init; }

    [JsonPropertyName("user")]
    public MaxUser? User { get; init; }

    [JsonPropertyName("is_channel")]
    public bool IsChannel { get; init; }
}