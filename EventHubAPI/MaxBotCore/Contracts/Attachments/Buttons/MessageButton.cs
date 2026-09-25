using System.Text.Json.Serialization;
namespace MaxBotCore.Contracts.Attachments.Buttons;
/// <summary>
/// Кнопка, отправляющая заранее заданный текст от имени пользователя в чат.
/// Полезна для быстрых ответов.
/// </summary>
public sealed class MessageButton : MaxButton
{
}