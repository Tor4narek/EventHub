using MaxBotCore.Contracts.Updates;
using MaxBotCore.Routing;
using MaxBotCore.Work;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Services.Interfaces;
using Storage.Entities;
using Xunit;

namespace MaxBotCore.Tests.Work;

public sealed class BotUpdateWorkerTests
{
	[Fact]
	public async Task Persisted_update_is_routed_then_marked_processed()
	{
		var update = new BotUpdate
		{
			Id = "id", Payload = """{"update_type":"bot_started","timestamp":1,"chat_id":42,"user":{"user_id":42}}"""
		};
		var inbox = new Mock<IBotUpdateInboxService>();
		inbox.Setup(x => x.GetAvailableAsync(It.IsAny<DateTime>(), 20, It.IsAny<CancellationToken>()))
			.ReturnsAsync([update]);
		inbox.Setup(x => x.TryClaimAsync("id", It.IsAny<DateTime>(), It.IsAny<DateTime>(),
			It.IsAny<CancellationToken>())).ReturnsAsync(true);
		var router = new Mock<IMaxUpdateRouter>();
		var services = new ServiceCollection()
			.AddSingleton(inbox.Object)
			.AddSingleton(router.Object)
			.BuildServiceProvider();
		var worker = new BotUpdateWorker(services.GetRequiredService<IServiceScopeFactory>(),
			NullLogger<BotUpdateWorker>.Instance);

		await worker.ProcessBatchAsync(CancellationToken.None);

		router.Verify(x => x.RouteAsync(It.Is<BotStartedUpdate>(u => u.User.UserId == 42),
			It.IsAny<CancellationToken>()), Times.Once);
		inbox.Verify(x => x.MarkProcessedAsync("id", It.IsAny<DateTime>(),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Failed_update_is_scheduled_for_retry()
	{
		var update = new BotUpdate
		{
			Id = "id", Payload = """{"update_type":"bot_started","timestamp":1,"chat_id":42,"user":{"user_id":42}}"""
		};
		var inbox = new Mock<IBotUpdateInboxService>();
		inbox.Setup(x => x.GetAvailableAsync(It.IsAny<DateTime>(), 20, It.IsAny<CancellationToken>()))
			.ReturnsAsync([update]);
		inbox.Setup(x => x.TryClaimAsync("id", It.IsAny<DateTime>(), It.IsAny<DateTime>(),
			It.IsAny<CancellationToken>())).ReturnsAsync(true);
		var router = new Mock<IMaxUpdateRouter>();
		router.Setup(x => x.RouteAsync(It.IsAny<MaxUpdate>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("MAX unavailable"));
		var services = new ServiceCollection()
			.AddSingleton(inbox.Object)
			.AddSingleton(router.Object)
			.BuildServiceProvider();
		var worker = new BotUpdateWorker(services.GetRequiredService<IServiceScopeFactory>(),
			NullLogger<BotUpdateWorker>.Instance);

		await worker.ProcessBatchAsync(CancellationToken.None);

		inbox.Verify(x => x.RecordFailureAsync("id", It.Is<DateTime>(time => time > DateTime.UtcNow),
			It.IsAny<CancellationToken>()), Times.Once);
		inbox.Verify(x => x.MarkProcessedAsync(It.IsAny<string>(), It.IsAny<DateTime>(),
			It.IsAny<CancellationToken>()), Times.Never);
	}
}
