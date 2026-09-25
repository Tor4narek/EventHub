using System.Text.Json.Serialization;

namespace MaxBotCore.Contracts.Attachments;

/// <summary>
/// Вложение с изображением. Либо загружается через POST /uploads и передаётся token,
/// либо передаётся прямая ссылка через url (только для изображений).
/// </summary>
public sealed class ImageAttachment : MaxAttachment
{
    [JsonPropertyName("payload")]
    public required ImageAttachmentPayload Payload { get; init; }
}

public sealed class ImageAttachmentPayload
{
    [JsonPropertyName("token")]
    public string? Token { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}