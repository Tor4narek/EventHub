using MaxBotCore.Contracts.Updates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MaxBotCore.Routing;

/// <summary>
/// Маршрутизатор Update. По типу пришедшего события резолвит из DI
/// подходящий IMaxUpdateHandler и вызывает его.
///
/// Плюсы такого подхода:
/// — Один switch по типу вместо десяти if'ов
/// — Добавление нового handler = регистрация в DI + новый класс, роутер не трогаем
/// — Каждый handler изолирован и легко тестируется
/// </summary>
public sealed class MaxUpdateRouter : IMaxUpdateRouter
{
	private readonly IServiceProvider _services;
	private readonly ILogger<MaxUpdateRouter> _logger;

	public MaxUpdateRouter(IServiceProvider services, ILogger<MaxUpdateRouter> logger)
	{
		_services = services;
		_logger = logger;
	}

	public Task RouteAsync(MaxUpdate update, CancellationToken cancellationToken)
	{
		_logger.LogInformation(
			"Получен Update от MAX. Тип: {UpdateType}, Timestamp: {Timestamp}",
			update.UpdateType, update.Timestamp);

		return update switch
		{
			MessageCreatedUpdate m => Dispatch(m, cancellationToken),
			MessageCallbackUpdate c => Dispatch(c, cancellationToken),
			BotStartedUpdate s => Dispatch(s, cancellationToken),
			BotStoppedUpdate s => Dispatch(s, cancellationToken),
			BotAddedUpdate a => Dispatch(a, cancellationToken),
			BotRemovedUpdate r => Dispatch(r, cancellationToken),
			_ => IgnoreUnknown(update)
		};
	}

	private Task Dispatch<TUpdate>(TUpdate update, CancellationToken cancellationToken)
		where TUpdate : MaxUpdate
	{
		// Получаем handler из scope'а текущего запроса.
		var handler = _services.GetService<IMaxUpdateHandler<TUpdate>>();
		if (handler is null)
		{
			_logger.LogWarning(
				"Не зарегистрирован IMaxUpdateHandler<{Type}> — событие проигнорировано.",
				typeof(TUpdate).Name);
			return Task.CompletedTask;
		}
		return handler.HandleAsync(update, cancellationToken);
	}

	private Task IgnoreUnknown(MaxUpdate update)
	{
		_logger.LogInformation(
			"Тип события '{UpdateType}' не поддерживается — игнорируем.",
			update.UpdateType);
		return Task.CompletedTask;
	}
}