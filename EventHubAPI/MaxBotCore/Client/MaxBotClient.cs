using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using MaxBotCore.Configuration;
using MaxBotCore.Contracts.Attachments;
using MaxBotCore.Contracts.Common;
using MaxBotCore.Contracts.Messages;
using MaxBotCore.Contracts.Requests;
using MaxBotCore.Contracts.Responses;
using MaxBotCore.Contracts.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MaxBotCore.Client;

/// <summary>
/// Реализация клиента MAX Bot API на базе HttpClient.
/// Регистрируется через AddHttpClient — тайм-аут и BaseAddress задаются при регистрации.
/// </summary>
public sealed class MaxBotClient : IMaxBotClient
{
	private readonly HttpClient _httpClient;
	private readonly ILogger<MaxBotClient> _logger;
	private readonly JsonSerializerOptions _jsonOptions = MaxJsonSerializerOptions.Default;

	public MaxBotClient(HttpClient httpClient, ILogger<MaxBotClient> logger)
	{
		_httpClient = httpClient;
		_logger = logger;
	}

	public async Task<MaxUser> GetMeAsync(CancellationToken cancellationToken)
	{
		using var response = await _httpClient.GetAsync("me", cancellationToken).ConfigureAwait(false);
		await EnsureSuccessAsync(response, "GET /me", cancellationToken).ConfigureAwait(false);
		var user = await response.Content.ReadFromJsonAsync<MaxUser>(_jsonOptions, cancellationToken)
			.ConfigureAwait(false);
		return user ?? throw new InvalidOperationException("MAX вернул пустое тело в ответ на GET /me.");
	}

	public Task<MaxMessage> SendMessageToUserAsync(
		long userId,
		string? text,
		IReadOnlyList<MaxAttachment>? attachments = null,
		TextFormat? format = null,
		bool notify = true,
		CancellationToken cancellationToken = default)
		=> SendMessageAsync($"messages?user_id={userId}", text, attachments, format, notify, cancellationToken);

	public Task<MaxMessage> SendMessageToChatAsync(
		long chatId,
		string? text,
		IReadOnlyList<MaxAttachment>? attachments = null,
		TextFormat? format = null,
		bool notify = true,
		CancellationToken cancellationToken = default)
		=> SendMessageAsync($"messages?chat_id={chatId}", text, attachments, format, notify, cancellationToken);

	public async Task<SubscriptionResponse> CreateSubscriptionAsync(
		string webhookUrl,
		IReadOnlyList<string>? updateTypes = null,
		string? secret = null,
		CancellationToken cancellationToken = default)
	{
		var request = new CreateSubscriptionRequest
		{
			Url = webhookUrl,
			UpdateTypes = updateTypes,
			Secret = secret
		};
		using var response = await _httpClient
			.PostAsJsonAsync("subscriptions", request, _jsonOptions, cancellationToken)
			.ConfigureAwait(false);
		await EnsureSuccessAsync(response, "POST /subscriptions", cancellationToken).ConfigureAwait(false);
		var payload = await response.Content
			.ReadFromJsonAsync<SubscriptionResponse>(_jsonOptions, cancellationToken)
			.ConfigureAwait(false);
		return payload ?? throw new InvalidOperationException("MAX вернул пустое тело в ответ на POST /subscriptions.");
	}

	public async Task AnswerCallbackAsync(string callbackId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(callbackId);
		using var response = await _httpClient
			.PostAsJsonAsync($"answers?callback_id={Uri.EscapeDataString(callbackId)}", new { }, _jsonOptions, cancellationToken)
			.ConfigureAwait(false);
		await EnsureSuccessAsync(response, "POST /answers", cancellationToken).ConfigureAwait(false);
		using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken)
			.ConfigureAwait(false);
		if (payload?.RootElement.TryGetProperty("success", out var success) == true && success.ValueKind == JsonValueKind.False)
		{
			throw new InvalidOperationException("MAX не подтвердил callback.");
		}
	}

	public async Task EditMessageAsync(string messageId, string text, IReadOnlyList<MaxAttachment> attachments,
		CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		using var response = await _httpClient.PutAsJsonAsync(
			$"messages?message_id={Uri.EscapeDataString(messageId)}",
			new SendMessageRequest { Text = text, Attachments = attachments, Notify = false }, _jsonOptions, cancellationToken);
		await EnsureSuccessAsync(response, "PUT /messages", cancellationToken);
		using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
		if (payload?.RootElement.TryGetProperty("success", out var success) != true || success.ValueKind != JsonValueKind.True)
			throw new HttpRequestException("MAX не подтвердил обновление сообщения.");
	}

	public async Task<bool> TryDeleteMessageAsync(string messageId, CancellationToken cancellationToken = default)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		using var response = await _httpClient.DeleteAsync($"messages?message_id={Uri.EscapeDataString(messageId)}", cancellationToken);
		if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) return false;
		await EnsureSuccessAsync(response, "DELETE /messages", cancellationToken);
		using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
		return payload?.RootElement.TryGetProperty("success", out var success) == true && success.ValueKind == JsonValueKind.True;
	}

	private async Task<MaxMessage> SendMessageAsync(
		string relativeUrl,
		string? text,
		IReadOnlyList<MaxAttachment>? attachments,
		TextFormat? format,
		bool notify,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(text) && (attachments is null || attachments.Count == 0))
		{
			throw new ArgumentException("Сообщение должно содержать текст или хотя бы одно вложение.");
		}
		var body = new SendMessageRequest
		{
			Text = text,
			Attachments = attachments,
			Format = format,
			Notify = notify
		};
		using var response = await _httpClient
			.PostAsJsonAsync(relativeUrl, body, _jsonOptions, cancellationToken)
			.ConfigureAwait(false);
		await EnsureSuccessAsync(response, $"POST /{relativeUrl}", cancellationToken).ConfigureAwait(false);
		var envelope = await response.Content
			.ReadFromJsonAsync<SendMessageResponse>(_jsonOptions, cancellationToken)
			.ConfigureAwait(false);
		return envelope?.Message
			?? throw new InvalidOperationException("MAX вернул ответ без поля message.");
	}

	/// <summary>
	/// Проверяет HTTP-статус ответа. При ошибке — логирует тело и бросает
	/// типизированное исключение, соответствующее семантике эндпоинта MAX.
	/// </summary>
	private async Task EnsureSuccessAsync(
		HttpResponseMessage response,
		string operation,
		CancellationToken cancellationToken)
	{
		if (response.IsSuccessStatusCode)
		{
			return;
		}
		var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
		_logger.LogWarning(
			"MAX API вернул ошибку. Операция: {Operation}, Статус: {Status}, Тело: {Body}",
			operation, (int)response.StatusCode, body);
		throw response.StatusCode switch
		{
			HttpStatusCode.Unauthorized => new InvalidOperationException(
				$"MAX API: ошибка авторизации (проверьте BotToken). Операция: {operation}. Тело: {body}"),
			HttpStatusCode.NotFound => new KeyNotFoundException(
				$"MAX API: ресурс не найден. Операция: {operation}. Тело: {body}"),
			HttpStatusCode.TooManyRequests => new InvalidOperationException(
				$"MAX API: превышено количество запросов (rate limit). Операция: {operation}."),
			_ => new HttpRequestException(
				$"MAX API вернул {(int)response.StatusCode}. Операция: {operation}. Тело: {body}")
		};
	}
}
