using System.Text.Json.Serialization;

namespace MaxBotCore.Contracts.Requests;

/// <summary>
/// Тело POST /subscriptions — регистрация webhook-endpoint.
/// </summary>
public sealed class CreateSubscriptionRequest
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>
    /// Список типов событий, которые хочет получать бот. См. Update types.
    /// </summary>
    [JsonPropertyName("update_types")]
    public IReadOnlyList<string>? UpdateTypes { get; init; }

    /// <summary>
    /// Секрет, приходящий в заголовке X-Max-Bot-Api-Secret каждого webhook-запроса.
    /// Строго рекомендуется, чтобы отличать запросы от MAX и от сторонних клиентов.
    /// Разрешены только A-Z, a-z, 0-9, дефис. Длина 5–256.
    /// </summary>
    [JsonPropertyName("secret")]
    public string? Secret { get; init; }
}