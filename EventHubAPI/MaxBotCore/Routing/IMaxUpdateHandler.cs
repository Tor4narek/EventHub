using MaxBotCore.Contracts.Updates;
namespace MaxBotCore.Routing;
/// <summary>
/// Обработчик одного конкретного типа Update. Регистрируется в DI как Scoped —
/// один экземпляр на webhook-запрос. Роутер выбирает handler по типу TUpdate.
/// </summary>
/// <typeparam name="TUpdate">Конкретный подтип MaxUpdate.</typeparam>
public interface IMaxUpdateHandler<in TUpdate> where TUpdate : MaxUpdate
{
    Task HandleAsync(TUpdate update, CancellationToken cancellationToken);
}