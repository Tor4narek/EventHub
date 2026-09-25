using MaxBotCore.Contracts.Updates;
using MaxBotCore.Routing;
using MaxBotCore.Scenarios;
using MaxBotCore.Client;
using Microsoft.Extensions.Logging;

namespace MaxBotCore.Handlers;

/// <summary>
/// Обработчик события message_callback — пользователь нажал inline-кнопку.
/// </summary>
public sealed class MessageCallbackHandler : IMaxUpdateHandler<MessageCallbackUpdate>
{
	private readonly IBotScenario _scenario;
	private readonly IMaxBotClient _client;
	private readonly ILogger<MessageCallbackHandler> _logger;

	public MessageCallbackHandler(IBotScenario scenario, IMaxBotClient client, ILogger<MessageCallbackHandler> logger)
	{
		_scenario = scenario;
		_client = client;
		_logger = logger;
	}

	public async Task HandleAsync(MessageCallbackUpdate update, CancellationToken cancellationToken)
	{
		if (!string.IsNullOrWhiteSpace(update.Callback.CallbackId))
		{
			try
			{
				await _client.AnswerCallbackAsync(update.Callback.CallbackId, cancellationToken);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "MAX не принял подтверждение callback {CallbackId}", update.Callback.CallbackId);
			}
		}
		if (update.Callback.User is not { IsBot: false, UserId: > 0 } user)
			return;
		await _scenario.HandleAsync(BotCommandParser.ParseCallback(user.UserId, update.Callback.Payload), cancellationToken);
	}
}
