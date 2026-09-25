using MaxBotCore.Contracts.Updates;
using MaxBotCore.Routing;
using MaxBotCore.Scenarios;

namespace MaxBotCore.Handlers;

/// <summary>
/// Обработчик события message_created — пользователь написал текст боту.
/// </summary>
public sealed class MessageCreatedHandler : IMaxUpdateHandler<MessageCreatedUpdate>
{
	private readonly IBotScenario _scenario;

	public MessageCreatedHandler(IBotScenario scenario)
	{
		_scenario = scenario;
	}

	public Task HandleAsync(MessageCreatedUpdate update, CancellationToken cancellationToken)
	{
		if (update.Message.Sender is not { IsBot: false, UserId: > 0 } sender)
			return Task.CompletedTask;
		return _scenario.HandleAsync(BotCommandParser.ParseText(sender.UserId, update.Message.Body?.Text), cancellationToken);
	}
}
