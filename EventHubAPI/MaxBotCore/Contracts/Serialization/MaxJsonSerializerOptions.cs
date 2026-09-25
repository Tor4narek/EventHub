using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MaxBotCore.Contracts.Serialization;

/// <summary>
/// Единая точка настройки System.Text.Json для сериализации/десериализации MAX API.
/// Используется и клиентом MAX, и контроллером webhook.
/// </summary>
public static class MaxJsonSerializerOptions
{
    public static readonly JsonSerializerOptions Default = CreateDefault();

    private static JsonSerializerOptions CreateDefault()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            // Не экранируем не-ASCII символы. Мы отправляем JSON как application/json,
            // а не встраиваем его в HTML — экранирование не нужно и мешает читать логи.
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return options;
    }
}