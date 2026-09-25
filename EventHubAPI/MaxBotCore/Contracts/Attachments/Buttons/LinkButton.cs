using System.Text.Json.Serialization;
namespace MaxBotCore.Contracts.Attachments.Buttons;
/// <summary>
/// Кнопка-ссылка. Открывает URL при нажатии.
/// </summary>
public sealed class LinkButton : MaxButton
{
    [JsonPropertyName("url")]
    public required string Url { get; init; }
}