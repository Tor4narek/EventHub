using System.Text.Json.Serialization;

namespace MaxBotCore.Contracts.Common;

/// <summary>
/// Пользователь или бот в MAX. Приходит в объекте Update и Message.
/// </summary>
public sealed class MaxUser
{
    [JsonPropertyName("user_id")]
    public long UserId { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("is_bot")]
    public bool IsBot { get; init; }

    [JsonPropertyName("last_activity_time")]
    public long? LastActivityTime { get; init; }
}