using MaxBotCore.Contracts.Updates;
using MaxBotCore.Routing;
using Microsoft.Extensions.Logging;

namespace MaxBotCore.Handlers;

/// <summary>
/// bot_removed — бот удалён из чата или канала. Наш сценарий этого не использует.
/// </summary>
public sealed class BotRemovedHandler : IMaxUpdateHandler<BotRemovedUpdate>
{
    private readonly ILogger<BotRemovedHandler> _logger;

    public BotRemovedHandler(ILogger<BotRemovedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(BotRemovedUpdate update, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[STUB] BotRemoved: ChatId={ChatId}", update.ChatId);
        return Task.CompletedTask;
    }
}