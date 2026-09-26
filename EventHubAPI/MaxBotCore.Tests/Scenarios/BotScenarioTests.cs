using MaxBotCore.Client;
using MaxBotCore.Configuration;
using MaxBotCore.Contracts.Attachments;
using MaxBotCore.Contracts.Attachments.Buttons;
using MaxBotCore.Contracts.Messages;
using MaxBotCore.Scenarios;
using Microsoft.Extensions.Options;
using Moq;
using Services.Interfaces;
using Storage.Entities;
using Xunit;

namespace MaxBotCore.Tests.Scenarios;

public sealed class BotScenarioTests
{
	[Fact]
	public async Task Start_without_tags_shows_onboarding()
	{
		var fixture = new Fixture();
		fixture.Users.Setup(x => x.GetUserTagIdsAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);
		fixture.Tags.Setup(x => x.GetTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
			[new Tag { Id = Guid.NewGuid(), Name = "Наука", Description = "", Examples = [] }]);

		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.Start), CancellationToken.None);

		fixture.Max.Verify(x => x.SendMessageToUserAsync(42, It.Is<string?>(s => s!.Contains("Выбери интересы")),
			It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, true, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
	}

	[Fact]
	public async Task Start_with_selected_tags_before_done_still_shows_onboarding()
	{
		var fixture = new Fixture();
		fixture.Users.Setup(x => x.GetUserTagIdsAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
			.ReturnsAsync([Guid.NewGuid()]);
		fixture.Tags.Setup(x => x.GetTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
			[new Tag { Id = Guid.NewGuid(), Name = "Наука", Description = "", Examples = [] }]);

		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.Start), CancellationToken.None);

		fixture.Max.Verify(x => x.SendMessageToUserAsync(42, "Главное меню",
			It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, true, It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Finishing_onboarding_uses_recommendation_service()
	{
		var fixture = new Fixture();
		fixture.Users.Setup(x => x.GetUserTagIdsAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
			.ReturnsAsync([Guid.NewGuid()]);
		fixture.Users.Setup(x => x.CompleteOnboardingAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
			.ReturnsAsync(true);
		fixture.Recommendations.Setup(x => x.GetTopEventsAsync(fixture.User.Id, 3,
			It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);

		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.FinishOnboarding), CancellationToken.None);

		fixture.Recommendations.VerifyAll();
		fixture.Max.Verify(x => x.SendMessageToUserAsync(42, "Главное меню",
			It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, true, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Repeated_done_does_not_send_recommendations_again()
	{
		var fixture = new Fixture();
		fixture.Users.Setup(x => x.GetUserTagIdsAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
			.ReturnsAsync([Guid.NewGuid()]);
		fixture.Users.Setup(x => x.CompleteOnboardingAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
			.ReturnsAsync(false);

		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.FinishOnboarding), CancellationToken.None);

		fixture.Recommendations.Verify(x => x.GetTopEventsAsync(It.IsAny<Guid>(), It.IsAny<int>(),
			It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
	}

	[Fact]
	public async Task Find_events_without_new_matches_offers_saved_events()
	{
		var fixture = new Fixture();
		fixture.User.HasCompletedOnboarding = true;
		fixture.Recommendations.Setup(x => x.GetTopEventsAsync(fixture.User.Id, 3,
			It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
			.ReturnsAsync([]);

		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.FindEvents), CancellationToken.None);

		fixture.Max.Verify(x => x.SendMessageToUserAsync(42,
			It.Is<string?>(s => s!.Contains("Новых мероприятий")),
			It.Is<IReadOnlyList<MaxAttachment>?>(attachments => attachments != null &&
				attachments.OfType<InlineKeyboardAttachment>().Any(keyboard =>
					keyboard.Payload.Buttons.SelectMany(row => row).OfType<CallbackButton>()
						.Any(button => button.Payload == "menu:saved"))),
			null, true, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Remind_uses_published_event_and_saves_it()
	{
		var fixture = new Fixture();
		var eventId = Guid.NewGuid();
		fixture.Events.Setup(x => x.GetEventByIdAsync(eventId, It.IsAny<CancellationToken>(), EventStatus.Published))
			.ReturnsAsync(new Event { Id = eventId });

		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.Remind, eventId), CancellationToken.None);

		fixture.Events.VerifyAll();
		fixture.Saved.Verify(x => x.SaveEventAsync(fixture.User.Id, eventId, It.IsAny<CancellationToken>()), Times.Once);
	}

	private sealed class Fixture
	{
		public readonly User User = new() { Id = Guid.NewGuid(), MaxUserId = 42 };
		public readonly Mock<IUserService> Users = new();
		public readonly Mock<ITagService> Tags = new();
		public readonly Mock<IRecomendationService> Recommendations = new();
		public readonly Mock<IUserEventService> Saved = new();
		public readonly Mock<IEventService> Events = new();
		public readonly Mock<IMaxBotClient> Max = new();
		public readonly BotScenario Scenario;

		public Fixture()
		{
			Users.Setup(x => x.CreateByMaxUserIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(User);
			Max.Setup(x => x.SendMessageToUserAsync(It.IsAny<long>(), It.IsAny<string?>(),
				It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, true, It.IsAny<CancellationToken>()))
				.ReturnsAsync(new MaxMessage());
			Scenario = new BotScenario(Users.Object, Tags.Object, Recommendations.Object, Saved.Object,
				Events.Object, Max.Object, Options.Create(new MaxOptions()));
		}
	}
}
