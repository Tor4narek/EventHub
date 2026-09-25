using System.Reflection;
using System.Text.Json.Serialization;

namespace MaxBotCore.Contracts.Updates;

/// <summary>
/// Базовый тип события (Update), приходящего от MAX через webhook или long polling.
/// Полиморфный: конкретный тип выбирается по значению поля "update_type".
/// Класс НЕ abstract специально: при получении неизвестного update_type
/// System.Text.Json создаёт экземпляр базового MaxUpdate — так бот не падает
/// на новых типах событий, а роутер просто игнорирует их (или логирует).
/// </summary>
[JsonPolymorphic(
    TypeDiscriminatorPropertyName = "update_type",
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType,
    IgnoreUnrecognizedTypeDiscriminators = true)]
[JsonDerivedType(typeof(MessageCreatedUpdate), "message_created")]
[JsonDerivedType(typeof(MessageCallbackUpdate), "message_callback")]
[JsonDerivedType(typeof(BotStartedUpdate), "bot_started")]
[JsonDerivedType(typeof(BotStoppedUpdate), "bot_stopped")]
[JsonDerivedType(typeof(BotAddedUpdate), "bot_added")]
[JsonDerivedType(typeof(BotRemovedUpdate), "bot_removed")]
public class MaxUpdate
{
    [JsonPropertyName("timestamp")]
    public long Timestamp { get; init; }

    /// <summary>
    /// Строковое имя типа события. Вычисляется из [JsonDerivedType] на базовом классе.
    /// Не сериализуется/не десериализуется. Для незнакомого типа возвращает "unknown".
    /// </summary>
    [JsonIgnore]
    public string UpdateType =>
        typeof(MaxUpdate)
            .GetCustomAttributes<JsonDerivedTypeAttribute>(inherit: false)
            .FirstOrDefault(a => a.DerivedType == GetType())?.TypeDiscriminator?.ToString()
        ?? "unknown";
}