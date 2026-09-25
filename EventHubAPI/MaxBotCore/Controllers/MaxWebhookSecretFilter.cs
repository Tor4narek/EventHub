using MaxBotCore.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MaxBotCore.Controllers;

/// <summary>
/// Фильтр валидации заголовка X-Max-Bot-Api-Secret на входящих webhook-запросах.
/// Если WebhookSecret в конфигурации задан — заголовок обязателен и должен совпадать.
/// Если не задан (локальная разработка без деплоя) — фильтр пропускает всё.
///
/// Используется как атрибут: [ServiceFilter(typeof(MaxWebhookSecretFilter))]
/// </summary>
public sealed class MaxWebhookSecretFilter : IAsyncActionFilter
{
    private const string SecretHeaderName = "X-Max-Bot-Api-Secret";

    private readonly IOptions<MaxOptions> _options;
    private readonly ILogger<MaxWebhookSecretFilter> _logger;

    public MaxWebhookSecretFilter(
        IOptions<MaxOptions> options,
        ILogger<MaxWebhookSecretFilter> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var expected = _options.Value.WebhookSecret;
        if (string.IsNullOrEmpty(expected))
        {
            // Секрет не настроен — режим локальной разработки, пропускаем.
            return next();
        }
        if (!context.HttpContext.Request.Headers.TryGetValue(SecretHeaderName, out var provided)
            || !string.Equals(provided.ToString(), expected, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                "Webhook-запрос отклонён: неверный или отсутствующий заголовок {Header}.",
                SecretHeaderName);
            context.Result = new UnauthorizedResult();
            return Task.CompletedTask;
        }
        return next();
    }
}