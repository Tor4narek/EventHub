using MaxBotCore.Client;
using MaxBotCore.Contracts.Attachments;
using MaxBotCore.Contracts.Messages;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Scheduler;
using Services.Interfaces;
using Storage.Entities;
using Xunit;

namespace MaxBotCore.Tests.Scheduler;

public sealed class SchedulerJobTests
{
	[Fact]
	public async Task Weekly_job_claims_subscriber_and_sends_recommendation()
	{
		var user = new User { Id = Guid.NewGuid(), MaxUserId = 42, IsWeeklyDigestEnabled = true };
		var users = new Mock<IUserService>();
		users.SetupSequence(x => x.GetWeeklyDigestSubscribersAsync(It.IsAny<DateTime>(), 100,
			It.IsAny<CancellationToken>()))
			.ReturnsAsync([user]).ReturnsAsync([]);
		users.Setup(x => x.TryClaimWeeklyDigestAsync(user.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(),
			It.IsAny<CancellationToken>())).ReturnsAsync(true);
		var recommendations = new Mock<IRecomendationService>();
		recommendations.Setup(x => x.GetTopEventsAsync(user.Id, 3, It.IsAny<DateTime>(),
			It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(
			[new Event { Id = Guid.NewGuid(), Title = "Лекция", Description = "Описание",
				Location = "ИТМО", Source = "https://example.com", EventDateTime = DateTime.UtcNow.AddDays(3) }]);
		var max = CreateMaxClient();
		var job = new WeeklyDigestJob(users.Object, recommendations.Object, max.Object,
			NullLogger<WeeklyDigestJob>.Instance);

		await job.RunAsync(new DateTime(2026, 9, 26, 22, 0, 0, DateTimeKind.Utc), CancellationToken.None);

		max.Verify(x => x.SendMessageToUserAsync(42, It.Is<string?>(s => s!.Contains("Лекция")),
			It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, true, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Reminder_job_skips_already_claimed_reminder()
	{
		var saved = new UserEvent
		{
			UserId = Guid.NewGuid(), EventId = Guid.NewGuid(),
			User = new User { MaxUserId = 42 },
			Event = new Event { Title = "Лекция", EventDateTime = DateTime.UtcNow.AddDays(1) }
		};
		var events = new Mock<IUserEventService>();
		events.SetupSequence(x => x.GetDueRemindersAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(),
			100, It.IsAny<CancellationToken>())).ReturnsAsync([saved]).ReturnsAsync([]);
		events.Setup(x => x.TryClaimReminderAsync(saved.UserId, saved.EventId,
			It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
		var max = CreateMaxClient();
		var job = new ReminderJob(events.Object, max.Object, NullLogger<ReminderJob>.Instance);

		await job.RunAsync(DateTime.UtcNow, CancellationToken.None);

		max.Verify(x => x.SendMessageToUserAsync(It.IsAny<long>(), It.IsAny<string?>(),
			It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, true, It.IsAny<CancellationToken>()), Times.Never);
	}

	private static Mock<IMaxBotClient> CreateMaxClient()
	{
		var max = new Mock<IMaxBotClient>();
		max.Setup(x => x.SendMessageToUserAsync(It.IsAny<long>(), It.IsAny<string?>(),
			It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, true, It.IsAny<CancellationToken>()))
			.ReturnsAsync(new MaxMessage());
		return max;
	}
}
