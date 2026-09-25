using MaxBotCore.Contracts.Updates;
using MaxBotCore.Routing;
using MaxBotCore.Scenarios;

namespace MaxBotCore.Handlers;

/// <summary>
/// Обработчик события bot_started — пользователь впервые открыл диалог с ботом
/// или возобновил после остановки. Триггер для запуска сценария онбординга.
/// </summary>
public sealed class BotStartedHandler : IMaxUpdateHandler<BotStartedUpdate>
{
	private readonly IBotScenario _scenario;

	public BotStartedHandler(IBotScenario scenario)
	{
		_scenario = scenario;
	}

	public Task HandleAsync(BotStartedUpdate update, CancellationToken cancellationToken) =>
		_scenario.HandleAsync(new BotCommand(update.User.UserId, BotCommandType.Start), cancellationToken);
}
