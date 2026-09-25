using System.Text.Json.Serialization;
using MaxBotCore.Contracts.Attachments.Buttons;

namespace MaxBotCore.Contracts.Attachments;

/// <summary>
/// Вложение inline-клавиатуры. Buttons — массив рядов, каждый ряд — массив кнопок.
/// Максимум 30 рядов, до 7 кнопок в ряду (до 3 для link/open_app/geo/contact).
/// </summary>
public sealed class InlineKeyboardAttachment : MaxAttachment
{
    [JsonPropertyName("payload")]
    public required InlineKeyboardPayload Payload { get; init; }
}

/// <summary>
/// Полезная нагрузка вложения inline-клавиатуры.
/// </summary>
public sealed class InlineKeyboardPayload
{
    [JsonPropertyName("buttons")]
    public required IReadOnlyList<IReadOnlyList<MaxButton>> Buttons { get; init; }
}