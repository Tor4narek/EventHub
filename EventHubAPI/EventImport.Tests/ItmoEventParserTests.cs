using System.Net;
using EventImport;
using Xunit;

namespace EventImport.Tests;

public class ItmoEventParserTests
{
	[Theory]
	[InlineData("https://example.com/events/1")]
	[InlineData("http://itmo.events/events/1")]
	[InlineData("https://itmo.events:8080/events/1")]
	[InlineData("https://itmo.events@127.0.0.1/events/1")]
	[InlineData("https://user@itmo.events/events/1")]
	[InlineData("https://itmo.events/")]
	[InlineData("https://itmo.events/events/1/../../admin")]
	public void RejectsUnsupportedUrls(string url) => Assert.Throws<ArgumentException>(() => new ItmoEventParser(new HttpClient()).NormalizeUrl(url));

	[Fact]
	public void RemovesTrackingParametersAndTrailingSlash()
	{
		Assert.Equal("https://itmo.events/events/123", new ItmoEventParser(new HttpClient()).NormalizeUrl("https://itmo.events/events/123/?utm_source=test#info").AbsoluteUri);
	}

	[Fact]
	public void KeepsDescriptionWithoutContactsOrProgrammeAndDoesNotGuessYear()
	{
		var result = ItmoEventParser.ParseHtml("""
			<h1>Научная встреча</h1><div>27 September, 11:00</div>
			<h3>О событии</h3><div><p>Первый абзац.</p><p>Второй абзац.</p><script>unsafe()</script><h4>Контактные лица</h4><p>Private email</p></div>
			<h3>Программа</h3><p>Extra programme</p><h3>Место проведения</h3><p>Санкт-Петербург, ул. Ломоносова, 9</p>
			""");
		Assert.Contains("Первый абзац.", result.Description);
		Assert.Contains("Второй абзац.", result.Description);
		Assert.DoesNotContain("Private", result.Description);
		Assert.DoesNotContain("unsafe", result.Description);
		Assert.DoesNotContain("Extra", result.Description);
		Assert.Null(result.EventDateTime);
		Assert.Null(result.Deadline);
		Assert.Contains("Ломоносова", result.Location);
	}

	[Fact]
	public void ReadsStructuredDatesAsUtcAndImage()
	{
		var result = ItmoEventParser.ParseHtml("""
			<h1>Event</h1><meta property="og:image" content="https://cdn.itmo.events/image.jpg">
			<script type="application/ld+json">{"@graph":[{"@type":"Event","startDate":"2028-10-29T17:00:00+03:00","description":"Science","location":{"name":"Online"}}]}</script>
			""");
		Assert.Equal(new DateTime(2028, 10, 29, 14, 0, 0, DateTimeKind.Utc), result.EventDateTime);
		Assert.Equal("Online", result.Location);
		Assert.Equal("https://cdn.itmo.events/image.jpg", result.MainImg);
	}

	[Fact]
	public void InvalidMetadataDoesNotLoseVisibleText()
	{
		var result = ItmoEventParser.ParseHtml("<h1>Title</h1><h3>О событии</h3><p>Visible description</p><script type='application/ld+json'>not-json</script>");
		Assert.Equal("Visible description", result.Description);
	}

	[Fact]
	public async Task RejectsRedirectWithoutFollowingIt()
	{
		var handler = new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("http://127.0.0.1/") } });
		var parser = new ItmoEventParser(new HttpClient(handler));
		await Assert.ThrowsAsync<ImportParseException>(() => parser.ParseAsync(new Uri("https://itmo.events/events/1"), default));
		Assert.Equal(1, handler.Calls);
	}

	[Fact]
	public async Task ParsesResponseAndIgnoresNonSourceImage()
	{
		var handler = new FakeHandler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<h1>Title</h1><meta property='og:image' content='http://localhost/image'><h3>О событии</h3><p>Text</p>", System.Text.Encoding.UTF8, "text/html") });
		var result = await new ItmoEventParser(new HttpClient(handler)).ParseAsync(new Uri("https://itmo.events/events/1"), default);
		Assert.Equal("Text", result.Description);
		Assert.Null(result.MainImg);
	}

	[Fact]
	public void ParsesActualItmoMarkupWithWrappedHeadingsAndNestedLocations()
	{
		var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "itmo-122043.html"));
		var result = ItmoEventParser.ParseHtml(html);
		Assert.Equal("Всероссийский День физики 2026", result.Title);
		Assert.Equal(new DateTime(2026, 9, 27, 8, 0, 0, DateTimeKind.Utc), result.EventDateTime);
		Assert.Equal("ул.Ломоносова, д.9", result.Location);
		Assert.Equal("https://cdn.itmo.events/covers/122043/cover.webp", result.MainImg);
		Assert.Contains("Вас ждут:", result.Description);
		Assert.Contains("\n", result.Description);
		Assert.DoesNotContain("Контактные лица", result.Description);
		Assert.Null(result.Deadline);
	}

	private sealed class FakeHandler(Func<HttpResponseMessage> response) : HttpMessageHandler
	{
		public int Calls { get; private set; }
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { Calls++; return Task.FromResult(response()); }
	}
}
