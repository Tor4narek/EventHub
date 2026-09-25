using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Storage;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
	public AppDbContext CreateDbContext(string[] args)
	{
		var apiProjectPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "");
		var envPath = Path.Combine(apiProjectPath, ".env");

		Env.Load(envPath);

		var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
		var connectionString = BuildPostgresConnectionString();

		optionsBuilder.UseNpgsql(connectionString);

		return new AppDbContext(optionsBuilder.Options);
	}

	public static string BuildPostgresConnectionString()
	{
		var host = Environment.GetEnvironmentVariable("POSTGRES_HOST");
		var port = Environment.GetEnvironmentVariable("POSTGRES_PORT");
		var database = Environment.GetEnvironmentVariable("POSTGRES_DB");
		var username = Environment.GetEnvironmentVariable("POSTGRES_USER");
		var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");

		if (string.IsNullOrWhiteSpace(host))
			throw new InvalidOperationException("POSTGRES_HOST is not set.");

		if (string.IsNullOrWhiteSpace(port))
			throw new InvalidOperationException("POSTGRES_PORT is not set.");

		if (string.IsNullOrWhiteSpace(database))
			throw new InvalidOperationException("POSTGRES_DB is not set.");

		if (string.IsNullOrWhiteSpace(username))
			throw new InvalidOperationException("POSTGRES_USER is not set.");

		if (string.IsNullOrWhiteSpace(password))
			throw new InvalidOperationException("POSTGRES_PASSWORD is not set.");

		return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
	}
}