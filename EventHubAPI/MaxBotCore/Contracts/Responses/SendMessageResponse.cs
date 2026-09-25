using System.Text.Json.Serialization;
using MaxBotCore.Contracts.Messages;

namespace MaxBotCore.Contracts.Responses;

/// <summary>
/// Ответ POST /messages. Внутри — созданное сообщение (обёртка вокруг Message).
/// </summary>
public sealed class SendMessageResponse
{
    [JsonPropertyName("message")]
    public MaxMessage? Message { get; init; }
}