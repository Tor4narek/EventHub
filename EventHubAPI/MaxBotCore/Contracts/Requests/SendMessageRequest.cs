using System.Text.Json.Serialization;
using MaxBotCore.Contracts.Attachments;
using MaxBotCore.Contracts.Common;

namespace MaxBotCore.Contracts.Requests;

/// <summary>
/// Тело POST /messages: текст, вложения, форматирование, флаг push-уведомлений.
/// Получатель (user_id или chat_id) передаётся не в теле, а в query-параметрах URL.
/// </summary>
public sealed class SendMessageRequest
{
    [JsonPropertyName("text")]
    public string? Text { get; init; }

    [JsonPropertyName("attachments")]
    public IReadOnlyList<MaxAttachment>? Attachments { get; init; }

    [JsonPropertyName("notify")]
    public bool? Notify { get; init; }

    [JsonPropertyName("format")]
    public TextFormat? Format { get; init; }
}