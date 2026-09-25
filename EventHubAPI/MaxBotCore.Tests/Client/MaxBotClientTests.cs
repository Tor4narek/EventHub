using System.Net;
using System.Text;
using MaxBotCore.Contracts.Attachments;
using MaxBotCore.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MaxBotCore.Tests.Client;

public sealed class MaxBotClientTests
{
	[Fact]
	public async Task Sent_message_with_unknown_response_attachment_is_not_retried()
	{
		var calls = 0;
		var handler = new FakeHandler(_ =>
		{
			calls++;
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("""
					{"message":{"body":{"mid":"sent-1","attachments":[{"type":"share","payload":{"url":"https://example.com"}}]}}}
					""", Encoding.UTF8, "application/json")
			};
		});
		var http = new HttpClient(handler) { BaseAddress = new Uri("https://platform-api2.max.ru/") };
		var client = new MaxBotClient(http, NullLogger<MaxBotClient>.Instance);

		var message = await client.SendMessageToUserAsync(42, "Карточка", cancellationToken: CancellationToken.None);

		Assert.Equal("sent-1", message.Body!.Mid);
		Assert.IsType<MaxAttachment>(Assert.Single(message.Body.Attachments!));
		Assert.Equal(1, calls);
	}

	[Fact]
	public async Task Answer_callback_posts_encoded_id_to_answers()
	{
		HttpRequestMessage? request = null;
		var handler = new FakeHandler(message =>
		{
			request = message;
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = new StringContent("{\"success\":true}", Encoding.UTF8, "application/json")
			};
		});
		var http = new HttpClient(handler) { BaseAddress = new Uri("https://platform-api2.max.ru/") };
		var client = new MaxBotClient(http, NullLogger<MaxBotClient>.Instance);

		await client.AnswerCallbackAsync("id+/=", CancellationToken.None);

		Assert.Equal(HttpMethod.Post, request!.Method);
		Assert.Equal("https://platform-api2.max.ru/answers?callback_id=id%2B%2F%3D", request.RequestUri!.ToString());
	}

	private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
			CancellationToken cancellationToken) => Task.FromResult(respond(request));
	}
}
