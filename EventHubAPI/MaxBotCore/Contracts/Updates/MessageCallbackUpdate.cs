using System.Text.Json.Serialization;
using MaxBotCore.Contracts.Messages;

namespace MaxBotCore.Contracts.Updates;

/// <summary>
/// Пользователь нажал inline-кнопку типа callback. В payload — то, что было заложено
/// разработчиком при отправке кнопки (например, идентификатор действия и его аргументы).
/// </summary>
public sealed class MessageCallbackUpdate : MaxUpdate
{
    [JsonPropertyName("callback")]
    public required MaxCallback Callback { get; init; }

    /// <summary>
    /// Сообщение, в котором была нажата кнопка (нужно, если хотим отредактировать его).
    /// </summary>
    [JsonPropertyName("message")]
    public MaxMessage? Message { get; init; }
}