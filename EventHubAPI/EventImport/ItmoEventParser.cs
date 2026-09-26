using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace EventImport;

public record ParsedEvent(string? Title, string? Description, DateTime? EventDateTime,
	string? Location, string? MainImg, DateTime? Deadline, string[] Warnings);

public interface IEventSourceParser
{
	Uri NormalizeUrl(string url);
	Task<ParsedEvent> ParseAsync(Uri url, CancellationToken cancellationToken);
}

// An optional model contributes suggestions; the original parsed text stays available to the administrator.
public record ImportSuggestions(string? Description, Guid[] TagIds);
public interface IEventImportEnricher
{
	Task<ImportSuggestions> SuggestAsync(ParsedEvent parsedEvent, CancellationToken cancellationToken);
}

public class ItmoEventParser(HttpClient client) : IEventSourceParser
{
	public Uri NormalizeUrl(string url)
	{
		if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
			uri.Scheme != Uri.UriSchemeHttps || uri.Host != "itmo.events" ||
			!uri.IsDefaultPort || uri.UserInfo.Length > 0 ||
			!Regex.IsMatch(uri.AbsolutePath, @"^/events/[1-9][0-9]{0,18}/?$"))
		{
			throw new ArgumentException("Укажите ссылку вида https://itmo.events/events/123456.");
		}
		return new Uri($"https://itmo.events/events/{uri.AbsolutePath.Trim('/').Split('/')[1]}");
	}

	public async Task<ParsedEvent> ParseAsync(Uri url, CancellationToken cancellationToken)
	{
		url = NormalizeUrl(url.AbsoluteUri);
		using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeout.CancelAfter(TimeSpan.FromSeconds(30));
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
				if ((int)response.StatusCode is >= 300 and < 400)
					throw new ImportParseException("Источник перенаправляет запрос. Проверьте прямую ссылку на мероприятие.");
				if ((int)response.StatusCode is 429 or >= 500 && attempt < 2)
				{
					await Task.Delay(TimeSpan.FromSeconds(attempt + 1), timeout.Token);
					continue;
				}
				if (!response.IsSuccessStatusCode)
					throw new ImportParseException($"Источник вернул HTTP {(int)response.StatusCode}.");
				if (response.Content.Headers.ContentType?.MediaType is not ("text/html" or "application/xhtml+xml"))
					throw new ImportParseException("Источник вернул не HTML-страницу.");
				await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
				using var output = new MemoryStream();
				var buffer = new byte[8192];
				int count;
				while ((count = await input.ReadAsync(buffer, timeout.Token)) > 0)
				{
					if (output.Length + count > 4 * 1024 * 1024) throw new ImportParseException("Страница превышает допустимый размер.");
					output.Write(buffer, 0, count);
				}
				return ParseHtml(System.Text.Encoding.UTF8.GetString(output.ToArray()));
			}
			catch (HttpRequestException) when (attempt < 2)
			{
				await Task.Delay(TimeSpan.FromSeconds(attempt + 1), timeout.Token);
			}
		}
	}

	public static ParsedEvent ParseHtml(string html)
	{
		var document = new HtmlParser().ParseDocument(html);
		string? Meta(string name) => document.QuerySelector($"meta[property='{name}'],meta[name='{name}']")?.GetAttribute("content");
		var title = Clean(document.QuerySelector("h1")?.TextContent ?? Meta("og:title"));
		if (string.IsNullOrWhiteSpace(title)) throw new ImportParseException("На странице не найдено название мероприятия.");
		if (title.Length > 300) title = title[..300];
		var description = Section(document, "О событии") ?? Clean(Meta("og:description") ?? Meta("description"));
		string? location = null;
		var image = SafeImage(Meta("og:image"));
		DateTime? date = null;
		DateTime? deadline = null;
		foreach (var script in document.QuerySelectorAll("script[type='application/ld+json']"))
		{
			try
			{
				using var json = JsonDocument.Parse(script.TextContent);
				foreach (var node in Events(json.RootElement))
				{
					date ??= Date(GetString(node, "startDate"));
					description ??= Clean(GetString(node, "description"));
					if (node.TryGetProperty("location", out var place))
						location ??= Clean(string.Join("; ", Places(place).Distinct()));
					if (node.TryGetProperty("image", out var picture))
						image ??= SafeImage(picture.ValueKind == JsonValueKind.String ? picture.GetString() : null);
				}
			}
			catch (JsonException) { /* Invalid optional metadata must not discard visible page content. */ }
		}
		location ??= Section(document, "Место проведения");
		date ??= Date(document.QuerySelector("time[datetime]")?.GetAttribute("datetime"));
		date ??= Date(document.QuerySelector("[itemprop='startDate']")?.GetAttribute("content"));
		deadline = Date(document.QuerySelector("[itemprop='registrationDeadline']")?.GetAttribute("content"));
		// A visible date without a year is ambiguous. Leave it for manual review instead of guessing.
		var warnings = new List<string>();
		if (date is null) warnings.Add("Дата и год не определены однозначно. Укажите дату и время вручную (МСК).");
		if (description is null) warnings.Add("Описание не найдено. Заполните его вручную.");
		if (location is null) warnings.Add("Место проведения не найдено. Укажите адрес или онлайн-формат.");
		if (deadline is null) warnings.Add("Дедлайн регистрации не указан в доступных данных. Проверьте первоисточник.");
		if (location?.Length > 300) { location = location[..300]; warnings.Add("Место проведения сокращено до 300 символов. Проверьте адрес."); }
		return new ParsedEvent(title, description, date, location, image, deadline, warnings.ToArray());
	}

	private static string? Section(IDocument document, string title)
	{
		var heading = document.QuerySelectorAll("h2,h3,h4").FirstOrDefault(e => Clean(e.TextContent)?.Equals(title, StringComparison.OrdinalIgnoreCase) == true);
		if (heading is null) return null;
		var parts = new List<string>();
		// ITMO wraps headings in .section-header; the content is its next sibling.
		var first = heading.NextElementSibling;
		if (first is null && heading.ParentElement?.ClassList.Contains("section-header") == true)
			first = heading.ParentElement.NextElementSibling;
		for (var sibling = first; sibling is not null; sibling = sibling.NextElementSibling)
		{
			if (Regex.IsMatch(sibling.LocalName, "^h[1-6]$")) break;
			if (sibling.LocalName is "script" or "style" or "form" or "button" or "iframe") continue;
			var clone = (IElement)sibling.Clone(true);
			var stop = clone.QuerySelector("h2,h3,h4");
			if (stop is not null)
			{
				// Description often shares a container with contact cards; keep only text before the next section.
				var prefix = clone.InnerHtml.Split(stop.OuterHtml, StringSplitOptions.None)[0];
				clone.InnerHtml = prefix;
			}
			foreach (var unsafeNode in clone.QuerySelectorAll("script,style,form,button,iframe")) unsafeNode.Remove();
			foreach (var br in clone.QuerySelectorAll("br")) br.Replace(clone.Owner!.CreateTextNode("\n"));
			foreach (var block in clone.QuerySelectorAll("p,li,div")) block.AppendChild(clone.Owner!.CreateTextNode("\n"));
			var text = Clean(clone.TextContent);
			if (text is not null) parts.Add(text);
			if (stop is not null) break;
		}
		return Clean(string.Join("\n\n", parts));
	}

	private static string? Clean(string? text)
	{
		if (string.IsNullOrWhiteSpace(text)) return null;
		return Regex.Replace(Regex.Replace(text.Replace('\u00a0', ' '), @"[^\S\r\n]+", " "), @"\n\s*\n\s*\n", "\n\n").Trim();
	}

	private static IEnumerable<string> Places(JsonElement node)
	{
		if (node.ValueKind == JsonValueKind.Array)
		{
			foreach (var child in node.EnumerateArray()) foreach (var place in Places(child)) yield return place;
		}
		else if (node.ValueKind == JsonValueKind.Object)
		{
			string? address = null;
			if (node.TryGetProperty("address", out var value))
				address = value.ValueKind == JsonValueKind.String ? value.GetString() : GetString(value, "streetAddress");
			var place = Clean(address ?? GetString(node, "name"));
			if (place is not null) yield return place;
		}
	}

	private static string? GetString(JsonElement node, string name) => node.ValueKind == JsonValueKind.Object && node.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
	private static IEnumerable<JsonElement> Events(JsonElement node)
	{
		if (node.ValueKind == JsonValueKind.Array) { foreach (var child in node.EnumerateArray()) foreach (var item in Events(child)) yield return item; }
		else if (node.ValueKind == JsonValueKind.Object)
		{
			if (GetString(node, "@type") is "Event" or "EducationEvent" or "SocialEvent") yield return node;
			if (node.TryGetProperty("@graph", out var graph)) foreach (var item in Events(graph)) yield return item;
		}
	}
	private static DateTime? Date(string? value)
	{
		if (string.IsNullOrWhiteSpace(value) || !Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}")) return null;
		if (!Regex.IsMatch(value, @"(Z|[+-]\d{2}:?\d{2})$")) value += "+03:00";
		return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.UtcDateTime : null;
	}
	private static string? SafeImage(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" && uri.Host == "cdn.itmo.events" && uri.IsDefaultPort && uri.UserInfo.Length == 0 && value.Length <= 2048 ? value : null;
}

public class ImportParseException(string message) : Exception(message);
