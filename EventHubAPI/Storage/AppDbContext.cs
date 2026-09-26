using Microsoft.EntityFrameworkCore;
using Storage.Entities;

namespace Storage;

public class AppDbContext : DbContext
{
	public DbSet<User> Users => Set<User>();
	public DbSet<UserTag> UserTags => Set<UserTag>();
	public DbSet<Tag> Tags => Set<Tag>();
	public DbSet<Event> Events => Set<Event>();
	public DbSet<EventTag> EventTags => Set<EventTag>();
	public DbSet<UserEvent> UserEvents => Set<UserEvent>();
	public DbSet<BotUpdate> BotUpdates => Set<BotUpdate>();
	public DbSet<EventImportRun> EventImportRuns => Set<EventImportRun>();
	public DbSet<EventImportItem> EventImportItems => Set<EventImportItem>();


	public AppDbContext(DbContextOptions<AppDbContext> options)
		: base(options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		base.OnModelCreating(modelBuilder);

		ConfigureUser(modelBuilder);
		ConfigureTag(modelBuilder);
		ConfigureEvent(modelBuilder);
		ConfigureUserTag(modelBuilder);
		ConfigureEventTag(modelBuilder);
		ConfigureUserEvent(modelBuilder);
		ConfigureBotUpdate(modelBuilder);
		ConfigureEventImport(modelBuilder);
	}

	private static void ConfigureUser(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<User>(entity =>
		{
			entity.ToTable("Users");

			entity.HasKey(e => e.Id);

			entity.HasIndex(e => e.MaxUserId)
				.IsUnique();

			entity.Property(e => e.IsWeeklyDigestEnabled)
				.HasDefaultValue(true);
			entity.Property(e => e.HasCompletedOnboarding)
				.HasDefaultValue(false);
		});
	}

	private static void ConfigureTag(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Tag>(entity =>
		{
			entity.ToTable("Tags");

			entity.HasKey(e => e.Id);

			entity.HasIndex(e => e.Name)
				.IsUnique();

			entity.Property(e => e.Name)
				.HasMaxLength(100)
				.IsRequired();

			entity.Property(e => e.Description)
				.IsRequired();
		});
	}

	private static void ConfigureEvent(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<Event>(entity =>
		{
			entity.ToTable("Events");

			entity.HasKey(e => e.Id);

			entity.Property(e => e.Title)
				.HasMaxLength(300)
				.IsRequired();

			entity.Property(e => e.Description)
				.IsRequired();

			entity.Property(e => e.Location)
				.HasMaxLength(300)
				.IsRequired();

			entity.Property(e => e.Source)
				.HasMaxLength(2048)
				.IsRequired();

			entity.Property(e => e.MainImg)
				.HasMaxLength(2048)
				.IsRequired(false);

			entity.Property(e => e.TagsConfirmed)
				.HasDefaultValue(true);
			entity.HasIndex(e => new { e.EventStatus,e.EventDateTime, e.Id });
			entity.HasIndex(e => e.Deadline);

			entity.Property(e => e.EventStatus)
				.HasConversion<string>()
				.HasMaxLength(20)
				.IsRequired();
		});
	}

	private static void ConfigureUserTag(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<UserTag>(entity =>
		{
			entity.ToTable("UserTags");

			entity.HasKey(e => new { e.UserId, e.TagId });

			entity.HasOne(e => e.User)
				.WithMany(e => e.UserTags)
				.HasForeignKey(e => e.UserId);

			entity.HasOne(e => e.Tag)
				.WithMany(e => e.UserTags)
				.HasForeignKey(e => e.TagId);
		});
	}

	private static void ConfigureEventTag(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<EventTag>(entity =>
		{
			entity.ToTable("EventTags");

			entity.HasKey(e => new { e.EventId, e.TagId });

			entity.HasOne(e => e.Event)
				.WithMany(e => e.Tags)
				.HasForeignKey(e => e.EventId);

			entity.HasOne(e => e.Tag)
				.WithMany()
				.HasForeignKey(e => e.TagId);
		});
	}

	private static void ConfigureUserEvent(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<UserEvent>(entity =>
		{
			entity.ToTable("UserEvents");
			entity.HasKey(e => new { e.UserId, e.EventId });

			entity.HasOne(e => e.User)
				.WithMany()
				.HasForeignKey(e => e.UserId);

			entity.HasOne(e => e.Event)
				.WithMany()
				.HasForeignKey(e => e.EventId);
		});
	}

	private static void ConfigureBotUpdate(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<BotUpdate>(entity =>
		{
			entity.ToTable("BotUpdates");
			entity.HasKey(e => e.Id);
			entity.Property(e => e.Id).HasMaxLength(64);
			entity.Property(e => e.Payload).HasColumnType("jsonb").IsRequired();
			entity.HasIndex(e => new { e.ProcessedAt, e.NextAttemptAt, e.LeaseUntil });
		});
	}
	private static void ConfigureEventImport(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<EventImportRun>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.HasMany(e => e.Items).WithOne().HasForeignKey(e => e.ImportRunId);
		});
		modelBuilder.Entity<EventImportItem>(entity =>
		{
			entity.HasKey(e => e.Id);
			entity.Property(e => e.SourceKey).HasMaxLength(100).IsRequired();
			entity.Property(e => e.Source).HasMaxLength(2048).IsRequired();
			entity.Property(e => e.Title).HasMaxLength(300);
			entity.Property(e => e.Location).HasMaxLength(300);
			entity.Property(e => e.MainImg).HasMaxLength(2048);
			entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
			entity.Property(e => e.LeaseToken).IsConcurrencyToken();
			entity.HasIndex(e => new { e.Status, e.LeaseUntil });
			entity.HasIndex(e => e.SourceKey).IsUnique()
				.HasFilter("\"Status\" = 'Confirmed'")
				.HasDatabaseName("IX_EventImportItems_ConfirmedSourceKey");
			entity.HasOne<Event>().WithMany().HasForeignKey(e => e.EventId).OnDelete(DeleteBehavior.Restrict);
		});
	}

}
