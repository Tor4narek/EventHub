using System.Net;
using System.Text;
using MaxBotCore.Client;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MaxBotCore.Tests.Client;

public sealed class MaxBotClientTests
{
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
