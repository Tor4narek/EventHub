using System.Text.Json;
using MaxBotCore.Contracts.Serialization;
using MaxBotCore.Contracts.Updates;
using MaxBotCore.Routing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace MaxBotCore.Controllers;

/// <summary>
/// Сохраняет обновление в БД до подтверждения доставки MAX.
/// </summary>
[ApiController]
[Route("api/max")]
[AllowAnonymous]
public sealed class MaxWebhookController : ControllerBase
{
	private readonly IBotUpdateInboxService _inbox;
	private readonly ILogger<MaxWebhookController> _logger;

	public MaxWebhookController(
		IBotUpdateInboxService inbox,
		ILogger<MaxWebhookController> logger)
	{
		_inbox = inbox;
		_logger = logger;
	}

	/// <summary>
	/// Приём Update от MAX. Тело запроса — JSON объекта Update.
	/// Проверяем JSON и сохраняем его для фоновой обработки.
	/// </summary>
	[HttpPost("webhook")]
	[RequestSizeLimit(1_000_000)]
	[ServiceFilter(typeof(MaxWebhookSecretFilter))]
	public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
	{
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(25));
		string payload;
		MaxUpdate? update;
		try
		{
			using var reader = new StreamReader(Request.Body);
			payload = await reader.ReadToEndAsync(timeout.Token);
			update = JsonSerializer.Deserialize<MaxUpdate>(payload, MaxJsonSerializerOptions.Default);
		}
		catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
		{
			return StatusCode(StatusCodes.Status503ServiceUnavailable);
		}
		catch (JsonException ex)
		{
			_logger.LogWarning(ex, "Не удалось распарсить тело webhook-запроса от MAX.");
			// Возвращаем 200, чтобы MAX не ретраил битый JSON бесконечно.
			return Ok();
		}
		if (update is null)
		{
			_logger.LogWarning("Пустое тело webhook-запроса от MAX.");
			return Ok();
		}
		try
		{
			await _inbox.EnqueueAsync(payload, timeout.Token);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex,
				"Не удалось сохранить Update типа {UpdateType}. MAX повторит доставку.",
				update.UpdateType);
			return StatusCode(StatusCodes.Status503ServiceUnavailable);
		}
		return Ok();
	}

	/// <summary>
	/// Health-check для проверки что бот-модуль жив.
	/// </summary>
	[HttpGet("health")]
	public IActionResult Health() => Ok(new { status = "ok", module = "MaxBotCore" });
}
