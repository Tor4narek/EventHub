using System.ComponentModel.DataAnnotations;

namespace MaxBotCore.Configuration;

/// <summary>
/// Настройки взаимодействия с MAX Bot API.
/// Читаются из конфигурации по секции "Max".
/// </summary>
public sealed class MaxOptions
{
    public const string SectionName = "Max";

    /// <summary>
    /// Токен доступа бота. В .env: Max__BotToken=...
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string BotToken { get; init; } = string.Empty;

    /// <summary>
    /// Базовый URL MAX API. По умолчанию production-домен platform-api2.max.ru.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ApiBaseUrl { get; init; } = "https://platform-api2.max.ru";

    /// <summary>
    /// Секрет для валидации webhook-запросов. Передаётся MAX в заголовке
    /// X-Max-Bot-Api-Secret. Опционально, но настоятельно рекомендуется.
    /// </summary>
    public string? WebhookSecret { get; init; }

    /// <summary>
    /// Тайм-аут HTTP-запросов к MAX API в секундах.
    /// </summary>
    [Range(1, 120)]
    public int HttpTimeoutSeconds { get; init; } = 30;

    /// <summary>Имя мини-приложения, подключённого к боту в MAX.</summary>
    public string? WebAppName { get; init; }
}
