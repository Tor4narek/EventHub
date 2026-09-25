using System.Text.Json.Serialization;
using MaxBotCore.Contracts.Common;
namespace MaxBotCore.Contracts.Updates;
/// <summary>
/// Пользователь впервые открыл диалог с ботом или возобновил после остановки.
/// Используется как триггер для запуска сценария онбординга.
/// </summary>
public sealed class BotStartedUpdate : MaxUpdate
{
    [JsonPropertyName("chat_id")]
    public long ChatId { get; init; }
    [JsonPropertyName("user")]
    public required MaxUser User { get; init; }
    [JsonPropertyName("user_locale")]
    public string? UserLocale { get; init; }
}