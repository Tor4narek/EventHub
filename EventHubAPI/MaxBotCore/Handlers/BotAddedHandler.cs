using MaxBotCore.Contracts.Updates;
using MaxBotCore.Routing;
using Microsoft.Extensions.Logging;

namespace MaxBotCore.Handlers;

/// <summary>
/// bot_added — бот добавлен в чат или канал. Наш сценарий этого не использует.
/// </summary>
public sealed class BotAddedHandler : IMaxUpdateHandler<BotAddedUpdate>
{
    private readonly ILogger<BotAddedHandler> _logger;

    public BotAddedHandler(ILogger<BotAddedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(BotAddedUpdate update, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[STUB] BotAdded: ChatId={ChatId}, IsChannel={IsChannel}",
            update.ChatId, update.IsChannel);
        return Task.CompletedTask;
    }
}