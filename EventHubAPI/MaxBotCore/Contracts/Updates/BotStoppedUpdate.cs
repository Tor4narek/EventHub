using System.Text.Json.Serialization;
using MaxBotCore.Contracts.Common;

namespace MaxBotCore.Contracts.Updates;

/// <summary>
/// Пользователь остановил или удалил бота. Можно использовать, чтобы выключить
/// рассылки для этого пользователя.
/// </summary>
public sealed class BotStoppedUpdate : MaxUpdate
{
    [JsonPropertyName("chat_id")]
    public long ChatId { get; init; }

    [JsonPropertyName("user")]
    public required MaxUser User { get; init; }
}