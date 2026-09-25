using System.Text.Json.Serialization;

namespace MaxBotCore.Contracts.Attachments;

/// <summary>
/// Базовый класс вложения к сообщению MAX. Тип различается по полю "type".
/// Известные типы: inline_keyboard (кнопки) и image (картинки).
/// Неизвестные типы в ответах MAX читаются как базовое вложение.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", IgnoreUnrecognizedTypeDiscriminators = true,
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(InlineKeyboardAttachment), "inline_keyboard")]
[JsonDerivedType(typeof(ImageAttachment), "image")]
public class MaxAttachment
{
}
