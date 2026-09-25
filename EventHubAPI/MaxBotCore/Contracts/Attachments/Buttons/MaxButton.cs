using System.Text.Json.Serialization;

namespace MaxBotCore.Contracts.Attachments.Buttons;

/// <summary>
/// Базовый класс кнопки inline-клавиатуры MAX. Полиморфный тип — конкретный класс
/// выбирается по значению поля "type" при десериализации/сериализации.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(CallbackButton), "callback")]
[JsonDerivedType(typeof(LinkButton), "link")]
[JsonDerivedType(typeof(MessageButton), "message")]
[JsonDerivedType(typeof(OpenAppButton), "open_app")]
public abstract class MaxButton
{
    [JsonPropertyName("text")]
    public required string Text { get; init; }
}
