using System.Text.Json.Serialization;

namespace MaxBotCore.Contracts.Attachments.Buttons;

/// <summary>
/// Кнопка типа callback. При нажатии MAX присылает Update типа message_callback
/// с полем payload, содержащим значение из этой кнопки.
/// </summary>
public sealed class CallbackButton : MaxButton
{
    /// <summary>
    /// Данные, которые придут обратно на сервер при нажатии.
    /// Максимум 1024 байта.
    /// </summary>
    [JsonPropertyName("payload")]
    public required string Payload { get; init; }
}