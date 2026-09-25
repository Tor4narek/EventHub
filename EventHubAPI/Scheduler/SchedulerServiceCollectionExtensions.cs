using Microsoft.Extensions.DependencyInjection;

namespace Scheduler;

public static class SchedulerServiceCollectionExtensions
{
	public static IServiceCollection AddBotScheduler(this IServiceCollection services)
	{
		services.AddScoped<WeeklyDigestJob>();
		services.AddScoped<ReminderJob>();
		services.AddHostedService<BotSchedulerService>();
		return services;
	}
}
