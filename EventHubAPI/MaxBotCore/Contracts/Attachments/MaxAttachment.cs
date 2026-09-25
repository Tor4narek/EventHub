using System.Text.Json.Serialization;

namespace MaxBotCore.Contracts.Attachments;

/// <summary>
/// Базовый класс вложения к сообщению MAX. Тип различается по полю "type".
/// Пока используем только два типа: inline_keyboard (кнопки) и image (картинки).
/// При необходимости легко добавить video, audio, file, share и т.д.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(InlineKeyboardAttachment), "inline_keyboard")]
[JsonDerivedType(typeof(ImageAttachment), "image")]
public abstract class MaxAttachment
{
}