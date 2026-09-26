using FluentAssertions;
using MaxBotCore.Contracts.Common;
using MaxBotCore.Contracts.Messages;
using MaxBotCore.Contracts.Updates;
using MaxBotCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MaxBotCore.Tests.Routing;

/// <summary>
/// Проверяет что роутер выбирает правильный handler по типу Update
/// и не падает на незнакомых типах.
/// </summary>
public sealed class MaxUpdateRouterTests
{
	[Fact]
	public async Task Callback_handler_passes_source_message_id_after_acknowledgement()
	{
		var client = new Mock<MaxBotCore.Client.IMaxBotClient>();
		var scenario = new Mock<MaxBotCore.Scenarios.IBotScenario>();
		var sequence = new MockSequence();
		client.InSequence(sequence).Setup(x => x.AnswerCallbackAsync("callback-1", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
		scenario.InSequence(sequence).Setup(x => x.HandleAsync(
			It.Is<MaxBotCore.Scenarios.BotCommand>(command => command.MessageId == "card-1" && command.MaxUserId == 42 && command.Type == MaxBotCore.Scenarios.BotCommandType.Settings),
			It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
		var handler = new MaxBotCore.Handlers.MessageCallbackHandler(scenario.Object, client.Object, NullLogger<MaxBotCore.Handlers.MessageCallbackHandler>.Instance);
		await handler.HandleAsync(new MessageCallbackUpdate
		{
			Callback = new MaxCallback { CallbackId = "callback-1", User = new MaxUser { UserId = 42 }, Payload = "menu:settings" },
			Message = new MaxMessage { Body = new MaxMessageBody { Mid = "card-1" } }
		}, CancellationToken.None);
		client.VerifyAll();
		scenario.VerifyAll();
	}

	[Fact]
	public async Task Router_dispatches_MessageCreated_to_correct_handler()
	{
		var handlerMock = new Mock<IMaxUpdateHandler<MessageCreatedUpdate>>();
		var services = new ServiceCollection()
			.AddSingleton(handlerMock.Object)
			.BuildServiceProvider();
		var router = new MaxUpdateRouter(services, NullLogger<MaxUpdateRouter>.Instance);
		var update = new MessageCreatedUpdate
		{
			Message = new MaxMessage { Timestamp = 1 }
		};

		await router.RouteAsync(update, CancellationToken.None);

		handlerMock.Verify(
			h => h.HandleAsync(update, It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task Router_dispatches_MessageCallback_to_correct_handler()
	{
		var handlerMock = new Mock<IMaxUpdateHandler<MessageCallbackUpdate>>();
		var services = new ServiceCollection()
			.AddSingleton(handlerMock.Object)
			.BuildServiceProvider();
		var router = new MaxUpdateRouter(services, NullLogger<MaxUpdateRouter>.Instance);
		var update = new MessageCallbackUpdate
		{
			Callback = new MaxCallback { Timestamp = 1, Payload = "test" }
		};

		await router.RouteAsync(update, CancellationToken.None);

		handlerMock.Verify(
			h => h.HandleAsync(update, It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task Router_dispatches_BotStarted_to_correct_handler()
	{
		var handlerMock = new Mock<IMaxUpdateHandler<BotStartedUpdate>>();
		var services = new ServiceCollection()
			.AddSingleton(handlerMock.Object)
			.BuildServiceProvider();
		var router = new MaxUpdateRouter(services, NullLogger<MaxUpdateRouter>.Instance);
		var update = new BotStartedUpdate
		{
			ChatId = 1,
			User = new MaxUser { UserId = 42 }
		};

		await router.RouteAsync(update, CancellationToken.None);

		handlerMock.Verify(
			h => h.HandleAsync(update, It.IsAny<CancellationToken>()),
			Times.Once);
	}

	[Fact]
	public async Task Router_does_not_throw_when_handler_is_not_registered()
	{
		// Пустой DI-контейнер — ни один handler не зарегистрирован.
		var services = new ServiceCollection().BuildServiceProvider();
		var router = new MaxUpdateRouter(services, NullLogger<MaxUpdateRouter>.Instance);
		var update = new MessageCreatedUpdate
		{
			Message = new MaxMessage { Timestamp = 1 }
		};

		// Не должно бросить — просто залогирует и вернёт.
		var act = () => router.RouteAsync(update, CancellationToken.None);
		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task Router_ignores_unknown_update_type()
	{
		var services = new ServiceCollection().BuildServiceProvider();
		var router = new MaxUpdateRouter(services, NullLogger<MaxUpdateRouter>.Instance);
		// Базовый MaxUpdate — то что вернёт десериализация для незнакомого update_type.
		var unknownUpdate = new MaxUpdate { Timestamp = 1 };

		var act = () => router.RouteAsync(unknownUpdate, CancellationToken.None);

		await act.Should().NotThrowAsync();
	}
}
