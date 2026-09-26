using EventImport;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Services;
using Services.Dto;
using Storage;
using Storage.Entities;
using Xunit;

namespace EventImport.Tests;

public sealed class PostgresFactAttribute : FactAttribute
{
	public PostgresFactAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EVENTHUB_IMPORT_TEST_CONNECTION")))
			Skip = "Set EVENTHUB_IMPORT_TEST_CONNECTION to run PostgreSQL integration tests in a temporary database.";
	}
}

public class ImportDatabase : IAsyncLifetime
{
	private string? adminConnection;
	private readonly string database = "eventhub_import_test_" + Guid.NewGuid().ToString("N");
	private string? connection;
	public async Task InitializeAsync()
	{
		adminConnection = Environment.GetEnvironmentVariable("EVENTHUB_IMPORT_TEST_CONNECTION");
		if (string.IsNullOrWhiteSpace(adminConnection)) return;
		await using var admin = new NpgsqlConnection(adminConnection);
		await admin.OpenAsync();
		await using var create = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", admin);
		await create.ExecuteNonQueryAsync();
		connection = new NpgsqlConnectionStringBuilder(adminConnection) { Database = database }.ConnectionString;
		await using var db = CreateContext();
		await db.Database.MigrateAsync();
	}
	public AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connection).Options);
	public async Task DisposeAsync()
	{
		if (connection is null) return;
		NpgsqlConnection.ClearAllPools();
		await using var admin = new NpgsqlConnection(adminConnection);
		await admin.OpenAsync();
		await using var drop = new NpgsqlCommand($"DROP DATABASE \"{database}\" WITH (FORCE)", admin);
		await drop.ExecuteNonQueryAsync();
	}
}

public class EventImportServiceTests(ImportDatabase database) : IClassFixture<ImportDatabase>
{
	private sealed class Parser : IEventSourceParser
	{
		public bool Fail { get; set; }
		public Uri NormalizeUrl(string url) => new ItmoEventParser(new HttpClient()).NormalizeUrl(url);
		public Task<ParsedEvent> ParseAsync(Uri url, CancellationToken cancellationToken) => Fail
			? throw new ImportParseException("Test failure")
			: Task.FromResult(new ParsedEvent("Test event", "Description", new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), "Online", null, null, []));
	}
	private static EventImportService Service(AppDbContext db, Parser parser) => new(db, parser, new EventService(db), []);

	[PostgresFact]
	public async Task ImportsCanonicalLinksAndConfirmsThroughExistingEventService()
	{
		await using var db = database.CreateContext(); var parser = new Parser(); var service = Service(db, parser);
		var tag = new Tag { Id = Guid.NewGuid(), Name = "Test " + Guid.NewGuid(), Description = "Science", Examples = [] };
		db.Tags.Add(tag); await db.SaveChangesAsync();
		var id = await service.StartImportAsync(["https://itmo.events/events/901/?utm_source=test", "https://itmo.events/events/901"], default);
		Assert.Single((await service.GetImportAsync(id, default)).Items);
		Assert.True(await service.ProcessNextAsync(default));
		var item = (await service.GetImportAsync(id, default)).Items.Single(); Assert.Equal(EventImportStatus.Ready, item.Status);
		await service.UpdateItemAsync(id, item.Id, new EventImportEditDto(item.Title, item.Description, item.EventDateTime, item.Location, null, [tag.Id]), default);
		var eventId = await service.ConfirmItemAsync(id, item.Id, default);
		Assert.Equal(eventId, await service.ConfirmItemAsync(id, item.Id, default));
		var created = await db.Events.Include(e => e.Tags).SingleAsync(e => e.Id == eventId);
		Assert.Equal(EventStatus.Draft, created.EventStatus); Assert.True(created.TagsConfirmed); Assert.Single(created.Tags); Assert.Null(created.MainImg);
	}

	[PostgresFact]
	public async Task ConcurrentConfirmationOfTwoRunsCreatesOneEvent()
	{
		var parser = new Parser(); Guid run1, run2, item1, item2;
		await using (var db = database.CreateContext())
		{
			var service = Service(db, parser); var tag = new Tag { Id = Guid.NewGuid(), Name = "Concurrent " + Guid.NewGuid(), Description = "Science", Examples = [] }; db.Tags.Add(tag); await db.SaveChangesAsync();
			run1 = await service.StartImportAsync(["https://itmo.events/events/902"], default); run2 = await service.StartImportAsync(["https://itmo.events/events/902"], default);
			await service.ProcessNextAsync(default); await service.ProcessNextAsync(default);
			var first = (await service.GetImportAsync(run1, default)).Items.Single(); var second = (await service.GetImportAsync(run2, default)).Items.Single(); item1 = first.Id; item2 = second.Id;
			foreach (var item in new[] { first, second }) await service.UpdateItemAsync(item.ImportRunId, item.Id, new EventImportEditDto(item.Title, item.Description, item.EventDateTime, item.Location, null, [tag.Id]), default);
		}
		await using var db1 = database.CreateContext(); await using var db2 = database.CreateContext();
		var results = await Task.WhenAll(Service(db1, parser).ConfirmItemAsync(run1, item1, default), Service(db2, parser).ConfirmItemAsync(run2, item2, default));
		Assert.Equal(results[0], results[1]); Assert.Equal(1, await db1.Events.CountAsync(e => e.Source == "https://itmo.events/events/902"));
	}

	[PostgresFact]
	public async Task FailureCanBeRetriedAndExpiredLeaseRecovers()
	{
		await using var db = database.CreateContext(); var parser = new Parser { Fail = true }; var service = Service(db, parser);
		var run = await service.StartImportAsync(["https://itmo.events/events/903"], default); await service.ProcessNextAsync(default);
		var item = (await service.GetImportAsync(run, default)).Items.Single(); Assert.Equal(EventImportStatus.Failed, item.Status);
		await service.RetryItemAsync(run, item.Id, default);
		await db.EventImportItems.Where(e => e.Id == item.Id).ExecuteUpdateAsync(set => set.SetProperty(e => e.Status, EventImportStatus.Processing).SetProperty(e => e.LeaseUntil, DateTime.UtcNow.AddMinutes(-1)).SetProperty(e => e.LeaseToken, Guid.NewGuid()));
		db.ChangeTracker.Clear(); parser.Fail = false; await service.ProcessNextAsync(default);
		item = await service.GetItemAsync(run, item.Id, default); Assert.Equal(EventImportStatus.Ready, item.Status); Assert.Null(item.LeaseUntil);
	}
}
