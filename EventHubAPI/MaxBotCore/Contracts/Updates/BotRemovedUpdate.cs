using System.Text.Json.Serialization;
using MaxBotCore.Contracts.Common;

namespace MaxBotCore.Contracts.Updates;

/// <summary>
/// Бот удалён из группового чата или канала.
/// </summary>
public sealed class BotRemovedUpdate : MaxUpdate
{
    [JsonPropertyName("chat_id")]
    public long ChatId { get; init; }

    [JsonPropertyName("user")]
    public MaxUser? User { get; init; }
}