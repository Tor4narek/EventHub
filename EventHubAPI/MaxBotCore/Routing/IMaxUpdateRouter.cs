using MaxBotCore.Contracts.Updates;

namespace MaxBotCore.Routing;

/// <summary>
/// Единая точка входа для обработки полученного от MAX Update.
/// Роутер сам определит конкретный подтип и вызовет соответствующий handler.
/// </summary>
public interface IMaxUpdateRouter
{
    Task RouteAsync(MaxUpdate update, CancellationToken cancellationToken);
}