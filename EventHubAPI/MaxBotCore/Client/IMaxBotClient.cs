using MaxBotCore.Contracts.Attachments;
using MaxBotCore.Contracts.Common;
using MaxBotCore.Contracts.Messages;
using MaxBotCore.Contracts.Responses;

namespace MaxBotCore.Client;

/// <summary>
/// Абстракция над MAX Bot API. Все методы возвращают доменные модели,
/// прячут детали HTTP и сериализации.
/// </summary>
public interface IMaxBotClient
{
    /// <summary>
    /// Получить информацию о боте (используется для проверки токена и как health-check).
    /// GET /me
    /// </summary>
    Task<MaxUser> GetMeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Отправить сообщение пользователю в личный диалог.
    /// POST /messages?user_id={userId}
    /// </summary>
    Task<MaxMessage> SendMessageToUserAsync(
        long userId,
        string? text,
        IReadOnlyList<MaxAttachment>? attachments = null,
        TextFormat? format = null,
        bool notify = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Отправить сообщение в чат или канал.
    /// POST /messages?chat_id={chatId}
    /// </summary>
    Task<MaxMessage> SendMessageToChatAsync(
        long chatId,
        string? text,
        IReadOnlyList<MaxAttachment>? attachments = null,
        TextFormat? format = null,
        bool notify = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Зарегистрировать webhook-endpoint.
    /// POST /subscriptions
    /// </summary>
    Task<SubscriptionResponse> CreateSubscriptionAsync(
        string webhookUrl,
        IReadOnlyList<string>? updateTypes = null,
        string? secret = null,
        CancellationToken cancellationToken = default);

    /// <summary>Подтвердить нажатие inline-кнопки. POST /answers.</summary>
    Task AnswerCallbackAsync(string callbackId, CancellationToken cancellationToken = default);
}
