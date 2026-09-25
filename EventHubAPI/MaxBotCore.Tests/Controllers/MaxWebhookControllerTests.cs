using System.Text;
using MaxBotCore.Contracts.Updates;
using MaxBotCore.Controllers;
using Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MaxBotCore.Tests.Controllers;

public sealed class MaxWebhookControllerTests
{
	[Fact]
	public async Task Valid_update_is_persisted_and_acknowledged()
	{
		var inbox = new Mock<IBotUpdateInboxService>();
		var controller = CreateController(inbox, """
			{"update_type":"bot_started","timestamp":1,"chat_id":42,"user":{"user_id":42}}
			""");

		var result = await controller.Webhook(CancellationToken.None);

		Assert.IsType<OkResult>(result);
		inbox.Verify(x => x.EnqueueAsync(It.Is<string>(s => s.Contains("bot_started")),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Unknown_update_is_acknowledged_without_failing()
	{
		var inbox = new Mock<IBotUpdateInboxService>();
		var controller = CreateController(inbox, """
			{"update_type":"future_event","timestamp":1}
			""");

		var result = await controller.Webhook(CancellationToken.None);

		Assert.IsType<OkResult>(result);
		inbox.Verify(x => x.EnqueueAsync(It.IsAny<string>(),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Failed_persistence_returns_retryable_status()
	{
		var inbox = new Mock<IBotUpdateInboxService>();
		inbox.Setup(x => x.EnqueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("DB unavailable"));
		var controller = CreateController(inbox, """
			{"update_type":"bot_started","timestamp":1,"chat_id":42,"user":{"user_id":42}}
			""");

		var result = await controller.Webhook(CancellationToken.None);

		Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsType<StatusCodeResult>(result).StatusCode);
	}

	private static MaxWebhookController CreateController(Mock<IBotUpdateInboxService> inbox, string json)
	{
		var http = new DefaultHttpContext();
		http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
		return new MaxWebhookController(inbox.Object, NullLogger<MaxWebhookController>.Instance)
		{
			ControllerContext = new ControllerContext { HttpContext = http }
		};
	}
}
