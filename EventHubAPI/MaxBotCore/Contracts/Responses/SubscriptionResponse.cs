using System.Text.Json.Serialization;

namespace MaxBotCore.Contracts.Responses;

/// <summary>
/// Ответ POST /subscriptions.
/// </summary>
public sealed class SubscriptionResponse
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}