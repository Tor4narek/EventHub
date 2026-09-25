using System.Runtime.Serialization;

namespace MaxBotCore.Contracts.Common;

/// <summary>
/// Формат разметки текста сообщения.
/// </summary>
public enum TextFormat
{
    [EnumMember(Value = "markdown")]
    Markdown,

    [EnumMember(Value = "html")]
    Html
}