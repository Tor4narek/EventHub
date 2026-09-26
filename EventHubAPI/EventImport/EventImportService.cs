using Microsoft.EntityFrameworkCore;
using Services.Dto;
using Services.Interfaces;
using Storage;
using Storage.Entities;

namespace EventImport;

public class EventImportService(AppDbContext db, IEventSourceParser parser, IEventService events,
	IEnumerable<IEventImportEnricher> enrichers) : IEventImportService
{
	public async Task<Guid> StartImportAsync(IReadOnlyCollection<string> urls, CancellationToken cancellationToken)
	{
		if (urls is null || urls.Count is < 1 or > 50) throw new ArgumentException("За один запуск можно импортировать от 1 до 50 ссылок.");
		var sources = urls.Select(parser.NormalizeUrl).Select(uri => uri.AbsoluteUri).Distinct().ToArray();
		var now = DateTime.UtcNow;
		var run = new EventImportRun { Id = Guid.NewGuid(), CreatedAt = now };
		run.Items = sources.Select(source => new EventImportItem
		{
			Id = Guid.NewGuid(), ImportRunId = run.Id, Source = source, SourceKey = SourceKey(source),
			Status = EventImportStatus.Pending, CreatedAt = now, UpdatedAt = now
		}).ToList();
		db.EventImportRuns.Add(run);
		await db.SaveChangesAsync(cancellationToken);
		return run.Id;
	}

	public async Task<IReadOnlyCollection<EventImportRun>> GetImportsAsync(CancellationToken cancellationToken) =>
		await db.EventImportRuns.AsNoTracking().Include(e => e.Items).OrderByDescending(e => e.CreatedAt).Take(20).ToListAsync(cancellationToken);

	public async Task<EventImportRun> GetImportAsync(Guid importId, CancellationToken cancellationToken) =>
		await db.EventImportRuns.AsNoTracking().Include(e => e.Items).FirstOrDefaultAsync(e => e.Id == importId, cancellationToken)
		?? throw new KeyNotFoundException("Запуск импорта не найден.");

	public async Task<EventImportItem> GetItemAsync(Guid importId, Guid itemId, CancellationToken cancellationToken) =>
		await db.EventImportItems.AsNoTracking().FirstOrDefaultAsync(e => e.Id == itemId && e.ImportRunId == importId, cancellationToken)
		?? throw new KeyNotFoundException("Результат импорта не найден.");

	public async Task<EventImportItem> UpdateItemAsync(Guid importId, Guid itemId, EventImportEditDto draft, CancellationToken cancellationToken)
	{
		await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
		var snapshot = await GetItemAsync(importId, itemId, cancellationToken);
		await LockSourceAsync(snapshot.SourceKey, cancellationToken);
		var item = await db.EventImportItems.SingleAsync(e => e.Id == itemId, cancellationToken);
		await db.Entry(item).ReloadAsync(cancellationToken);
		if (item.Status is not (EventImportStatus.Ready or EventImportStatus.Failed))
			throw new InvalidOperationException("Изменять можно только готовый результат или результат с ошибкой.");
		if (draft.TagIds is null || draft.TagIds.Length > 200) throw new ArgumentException("Передан некорректный список тегов.");
		if (draft.EventDateTime is { Kind: not DateTimeKind.Utc } || draft.Deadline is { Kind: not DateTimeKind.Utc })
			throw new ArgumentException("Передавайте даты в UTC с суффиксом Z.");
		item.Title = draft.Title?.Trim(); item.Description = draft.Description?.Trim();
		item.Location = draft.Location?.Trim(); item.EventDateTime = draft.EventDateTime;
		item.Deadline = draft.Deadline; item.MainImg = draft.MainImg?.Trim(); item.TagIds = draft.TagIds.Distinct().ToArray();
		item.UpdatedAt = DateTime.UtcNow;
		// Manual corrections can rescue a page that could not be parsed.
		item.Status = EventImportStatus.Ready; item.Error = null;
		await db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return item;
	}

	public async Task<Guid> ConfirmItemAsync(Guid importId, Guid itemId, CancellationToken cancellationToken)
	{
		await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
		var snapshot = await GetItemAsync(importId, itemId, cancellationToken);
		await LockSourceAsync(snapshot.SourceKey, cancellationToken);
		var item = await db.EventImportItems.SingleAsync(e => e.Id == itemId, cancellationToken);
		await db.Entry(item).ReloadAsync(cancellationToken);
		if (item.EventId is { } confirmed) return confirmed;
		if (item.Status != EventImportStatus.Ready) throw new InvalidOperationException("Сначала дождитесь загрузки и сохраните проверенные данные.");
		var existing = await FindExistingEventAsync(item.SourceKey, cancellationToken);
		if (existing is { } duplicate)
		{
			item.EventId = duplicate; item.Status = EventImportStatus.Duplicate; item.UpdatedAt = DateTime.UtcNow;
			await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
			return duplicate;
		}
		if (item.TagIds.Length == 0) throw new ArgumentException("Выберите хотя бы один тег мероприятия.");
		if (item.EventDateTime is null) throw new ArgumentException("Укажите дату и время мероприятия.");
		var created = await events.CreateEventAsync(new EventDto(item.Title ?? "", item.Description ?? "",
			item.EventDateTime.Value, item.Location ?? "", item.Source, item.Deadline, item.TagIds, item.MainImg), cancellationToken);
		item.EventId = created.Id; item.Status = EventImportStatus.Confirmed; item.UpdatedAt = DateTime.UtcNow;
		await db.SaveChangesAsync(cancellationToken);
		await transaction.CommitAsync(cancellationToken);
		return created.Id;
	}

	public async Task RetryItemAsync(Guid importId, Guid itemId, CancellationToken cancellationToken)
	{
		var affected = await db.EventImportItems.Where(e => e.ImportRunId == importId && e.Id == itemId && e.Status == EventImportStatus.Failed)
			.ExecuteUpdateAsync(setters => setters.SetProperty(e => e.Status, EventImportStatus.Pending)
				.SetProperty(e => e.Error, (string?)null).SetProperty(e => e.UpdatedAt, DateTime.UtcNow), cancellationToken);
		if (affected == 0) { await GetItemAsync(importId, itemId, cancellationToken); throw new InvalidOperationException("Повторно загрузить можно только результат с ошибкой."); }
	}

	public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
	{
		var now = DateTime.UtcNow;
		var candidate = await db.EventImportItems.AsNoTracking()
			.Where(e => e.Status == EventImportStatus.Pending || e.Status == EventImportStatus.Processing && e.LeaseUntil < now)
			.OrderBy(e => e.CreatedAt).ThenBy(e => e.Id).FirstOrDefaultAsync(cancellationToken);
		if (candidate is null) return false;
		var lease = Guid.NewGuid();
		var claimed = await db.EventImportItems.Where(e => e.Id == candidate.Id &&
			(e.Status == EventImportStatus.Pending || e.Status == EventImportStatus.Processing && e.LeaseUntil < now))
			.ExecuteUpdateAsync(setters => setters.SetProperty(e => e.Status, EventImportStatus.Processing)
				.SetProperty(e => e.LeaseToken, lease).SetProperty(e => e.LeaseUntil, now.AddMinutes(2)), cancellationToken);
		if (claimed == 0) return true;
		var item = await db.EventImportItems.SingleAsync(e => e.Id == candidate.Id, cancellationToken);
		await db.Entry(item).ReloadAsync(cancellationToken);
		if (item.LeaseToken != lease) return true;
		try
		{
			var existing = await FindExistingEventAsync(item.SourceKey, cancellationToken);
			if (existing is { } duplicate) { item.EventId = duplicate; item.Status = EventImportStatus.Duplicate; }
			else
			{
				var parsed = await parser.ParseAsync(new Uri(item.Source), cancellationToken);
				item.Title = parsed.Title; item.Description = parsed.Description; item.OriginalDescription = parsed.Description;
				item.Location = parsed.Location; item.MainImg = parsed.MainImg; item.EventDateTime = parsed.EventDateTime;
				item.Deadline = parsed.Deadline; item.Warnings = parsed.Warnings; item.Error = null;
				foreach (var enricher in enrichers)
				{
					using var modelTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
					modelTimeout.CancelAfter(TimeSpan.FromSeconds(15));
					try
					{
						var suggestions = await enricher.SuggestAsync(parsed, modelTimeout.Token).WaitAsync(modelTimeout.Token);
						item.SuggestedDescription = suggestions.Description;
						item.SuggestedTagIds = suggestions.TagIds;
					}
					catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is HttpRequestException or OperationCanceledException or ImportParseException)
					{
						item.Warnings = [.. item.Warnings, "Подсказки модели недоступны. Проверьте мероприятие вручную."];
					}
				}
				item.Status = EventImportStatus.Ready;
			}
		}
		catch (Exception ex) when (!cancellationToken.IsCancellationRequested && ex is HttpRequestException or OperationCanceledException or ImportParseException)
		{
			item.Status = EventImportStatus.Failed;
			item.Error = ex is ImportParseException ? ex.Message : ex is OperationCanceledException ? "Источник не ответил вовремя. Повторите загрузку." : "Не удалось загрузить страницу источника. Проверьте доступ к ITMO Events и повторите загрузку.";
		}
		item.LeaseToken = null; item.LeaseUntil = null; item.UpdatedAt = DateTime.UtcNow;
		// Parsing is bounded below the lease time; database faults propagate to the worker and are logged.
		await db.SaveChangesAsync(cancellationToken);
		return true;
	}

	private Task<int> LockSourceAsync(string key, CancellationToken cancellationToken) =>
		db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", cancellationToken);

	private async Task<Guid?> FindExistingEventAsync(string key, CancellationToken cancellationToken)
	{
		var imported = await db.EventImportItems.AsNoTracking().Where(e => e.SourceKey == key && e.Status == EventImportStatus.Confirmed)
			.Select(e => e.EventId).FirstOrDefaultAsync(cancellationToken);
		if (imported is not null) return imported;
		var candidates = await db.Events.AsNoTracking().Where(e => e.Source.Contains("itmo.events/events/"))
			.Select(e => new { e.Id, e.Source }).ToListAsync(cancellationToken);
		foreach (var entry in candidates)
		{
			try { if (SourceKey(parser.NormalizeUrl(entry.Source).AbsoluteUri) == key) return entry.Id; }
			catch (ArgumentException) { /* Other manually entered source URLs do not belong to this parser. */ }
		}
		return null;
	}

	private static string SourceKey(string source) => "itmo:" + new Uri(source).AbsolutePath.Trim('/').Split('/')[1];
}
