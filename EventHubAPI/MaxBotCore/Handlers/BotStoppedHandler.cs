using MaxBotCore.Contracts.Updates;
using MaxBotCore.Routing;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace MaxBotCore.Handlers;

/// <summary>
/// Обработчик события bot_stopped — пользователь остановил или удалил бота.
/// </summary>
public sealed class BotStoppedHandler : IMaxUpdateHandler<BotStoppedUpdate>
{
	private readonly ILogger<BotStoppedHandler> _logger;
	private readonly IUserService _users;

	public BotStoppedHandler(ILogger<BotStoppedHandler> logger, IUserService users)
	{
		_logger = logger;
		_users = users;
	}

	public async Task HandleAsync(BotStoppedUpdate update, CancellationToken cancellationToken)
	{
		try
		{
			var user = await _users.GetByMaxUserIdAsync(update.User.UserId, cancellationToken);
			await _users.SetWeeklyDigestAsync(user.Id, false, cancellationToken);
		}
		catch (KeyNotFoundException)
		{
			_logger.LogInformation("BotStopped: пользователь {UserId} не зарегистрирован.", update.User.UserId);
		}
	}
}
