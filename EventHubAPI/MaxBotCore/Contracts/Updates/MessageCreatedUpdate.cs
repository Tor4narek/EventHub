using System.Text.Json.Serialization;
using MaxBotCore.Contracts.Messages;

namespace MaxBotCore.Contracts.Updates;

/// <summary>
/// Пользователь отправил боту новое сообщение (или в чат/канал, где есть бот).
/// </summary>
public sealed class MessageCreatedUpdate : MaxUpdate
{
    [JsonPropertyName("message")]
    public required MaxMessage Message { get; init; }

    /// <summary>
    /// Идентификатор пользователя-получателя (когда бот получает сообщение в диалоге).
    /// </summary>
    [JsonPropertyName("user_locale")]
    public string? UserLocale { get; init; }
}