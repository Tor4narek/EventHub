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
			It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, false, It.IsAny<CancellationToken>()), Times.AtLeastOnce);
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
			It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, false, It.IsAny<CancellationToken>()), Times.Never);
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
			It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, false, It.IsAny<CancellationToken>()), Times.Once);
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
		fixture.Saved.Setup(x => x.GetSavedEventsAsync(fixture.User.Id, It.IsAny<CancellationToken>()))
			.ReturnsAsync([new Event { EventDateTime = DateTime.UtcNow.AddDays(2) }]);
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
			null, false, It.IsAny<CancellationToken>()), Times.Once);
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

	[Fact]
	public async Task Welcome_precedes_onboarding()
	{
		var fixture = new Fixture();
		fixture.Tags.Setup(x => x.GetTagsAsync(It.IsAny<CancellationToken>()))
			.ReturnsAsync([new Tag { Id = Guid.NewGuid(), Name = "Наука" }]);
		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.Start), CancellationToken.None);
		var messages = fixture.Max.Invocations.Where(call => call.Method.Name == nameof(IMaxBotClient.SendMessageToUserAsync)).ToList();
		Assert.Contains("Привет! Я EventHub", (string)messages[0].Arguments[1]);
		Assert.Contains("Выбери интересы", (string)messages[1].Arguments[1]);
	}

	[Fact]
	public async Task Interest_click_updates_same_message_without_chat_spam()
	{
		var fixture = new Fixture();
		var tag = new Tag { Id = Guid.NewGuid(), Name = "Наука" };
		fixture.Tags.Setup(x => x.GetTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([tag]);
		fixture.Users.Setup(x => x.UpdateUserTagsAsync(fixture.User.Id, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
			.Callback<Guid, IReadOnlyCollection<Guid>, CancellationToken>((_, ids, _) =>
				fixture.Users.Setup(x => x.GetUserTagIdsAsync(fixture.User.Id, It.IsAny<CancellationToken>())).ReturnsAsync(ids.ToList()));
		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.SetInterest, tag.Id, true, true, MessageId: "interests-1"), CancellationToken.None);
		fixture.Max.Verify(x => x.EditMessageAsync("interests-1", It.Is<string>(text => text.Contains("Выбрано: Наука")),
			It.IsAny<IReadOnlyList<MaxAttachment>>(), It.IsAny<CancellationToken>()), Times.Once);
		Assert.DoesNotContain(fixture.Max.Invocations, call => call.Method.Name == nameof(IMaxBotClient.SendMessageToUserAsync));
	}

	[Fact]
	public async Task Old_onboarding_button_does_not_change_completed_profile()
	{
		var fixture = new Fixture();
		fixture.User.HasCompletedOnboarding = true;
		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.SetInterest, Guid.NewGuid(), true, true, MessageId: "old-1"), CancellationToken.None);
		Assert.DoesNotContain(fixture.Users.Invocations, call => call.Method.Name == nameof(IUserService.UpdateUserTagsAsync));
		fixture.Max.Verify(x => x.EditMessageAsync("old-1", It.Is<string>(text => text.Contains("уже завершён")),
			It.IsAny<IReadOnlyList<MaxAttachment>>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Save_updates_card_and_reports_existing_save()
	{
		var fixture = new Fixture();
		var item = new Event { Id = Guid.NewGuid(), Title = "Лекция", EventDateTime = DateTime.UtcNow.AddDays(2) };
		fixture.Events.Setup(x => x.GetEventByIdAsync(item.Id, It.IsAny<CancellationToken>(), EventStatus.Published)).ReturnsAsync(item);
		fixture.Saved.Setup(x => x.IsEventSavedAsync(fixture.User.Id, item.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.Remind, item.Id, MessageId: "card-1"), CancellationToken.None);
		fixture.Max.Verify(x => x.EditMessageAsync("card-1", It.Is<string>(text => text.Contains("Уже в сохранённых")),
			It.Is<IReadOnlyList<MaxAttachment>>(attachments => attachments.OfType<InlineKeyboardAttachment>().Any(keyboard =>
				keyboard.Payload.Buttons.SelectMany(row => row).OfType<CallbackButton>().Any(button => button.Payload == "unsave:" + item.Id))),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Unavailable_event_has_recovery_buttons_and_is_not_saved()
	{
		var fixture = new Fixture();
		var id = Guid.NewGuid();
		fixture.Events.Setup(x => x.GetEventByIdAsync(id, It.IsAny<CancellationToken>(), EventStatus.Published)).ThrowsAsync(new KeyNotFoundException());
		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.Remind, id, MessageId: "card-1"), CancellationToken.None);
		Assert.DoesNotContain(fixture.Saved.Invocations, call => call.Method.Name == nameof(IUserEventService.SaveEventAsync));
		fixture.Max.Verify(x => x.EditMessageAsync("card-1", It.Is<string>(text => text.Contains("больше недоступно")),
			It.IsAny<IReadOnlyList<MaxAttachment>>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Deleted_message_falls_back_to_new_message_without_repeating_save()
	{
		var fixture = new Fixture();
		var item = new Event { Id = Guid.NewGuid(), EventDateTime = DateTime.UtcNow.AddHours(3) };
		fixture.Events.Setup(x => x.GetEventByIdAsync(item.Id, It.IsAny<CancellationToken>(), EventStatus.Published)).ReturnsAsync(item);
		fixture.Max.Setup(x => x.EditMessageAsync("deleted", It.IsAny<string>(), It.IsAny<IReadOnlyList<MaxAttachment>>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new KeyNotFoundException());
		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.Remind, item.Id, MessageId: "deleted"), CancellationToken.None);
		fixture.Saved.Verify(x => x.SaveEventAsync(fixture.User.Id, item.Id, It.IsAny<CancellationToken>()), Times.Once);
		fixture.Max.Verify(x => x.SendMessageToUserAsync(42, It.Is<string?>(text => text!.Contains("меньше суток")),
			It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, false, It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Unexpected_service_error_is_not_hidden_as_unavailable_event()
	{
		var fixture = new Fixture();
		var id = Guid.NewGuid();
		fixture.Events.Setup(x => x.GetEventByIdAsync(id, It.IsAny<CancellationToken>(), EventStatus.Published)).ThrowsAsync(new InvalidOperationException("unexpected"));
		await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.Remind, id), CancellationToken.None));
	}

	[Fact]
	public async Task Saved_defaults_to_future_and_offers_past_events()
	{
		var fixture = new Fixture();
		fixture.Saved.Setup(x => x.GetSavedEventsAsync(fixture.User.Id, It.IsAny<CancellationToken>())).ReturnsAsync([
			new Event { Title = "Будущее", EventDateTime = DateTime.UtcNow.AddDays(2) },
			new Event { Title = "Прошедшее", EventDateTime = DateTime.UtcNow.AddDays(-2) }]);
		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.SavedEvents), CancellationToken.None);
		var messages = fixture.Max.Invocations.Where(call => call.Method.Name == nameof(IMaxBotClient.SendMessageToUserAsync)).ToList();
		Assert.DoesNotContain(messages, call => ((string)call.Arguments[1]).Contains("Прошедшее"));
		Assert.Contains(messages, call => call.Arguments[2] is IReadOnlyList<MaxAttachment> attachments && attachments.OfType<InlineKeyboardAttachment>()
			.Any(keyboard => keyboard.Payload.Buttons.SelectMany(row => row).OfType<CallbackButton>().Any(button => button.Payload == "saved:past")));
	}

	[Fact]
	public async Task Digest_setting_updates_same_message()
	{
		var fixture = new Fixture();
		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.SetWeeklyDigest, Enabled: false, MessageId: "settings-1"), CancellationToken.None);
		fixture.Users.Verify(x => x.SetWeeklyDigestAsync(fixture.User.Id, false, It.IsAny<CancellationToken>()), Times.Once);
		fixture.Max.Verify(x => x.EditMessageAsync("settings-1", It.Is<string>(text => text.Contains("выключена") && text.Contains("не отключает напоминания")),
			It.IsAny<IReadOnlyList<MaxAttachment>>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Remove_updates_card_with_restore_action()
	{
		var fixture = new Fixture();
		var item = new Event { Id = Guid.NewGuid(), EventDateTime = DateTime.UtcNow.AddDays(2) };
		fixture.Events.Setup(x => x.GetEventByIdAsync(item.Id, It.IsAny<CancellationToken>(), EventStatus.Published)).ReturnsAsync(item);
		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.RemoveReminder, item.Id, MessageId: "card-1"), CancellationToken.None);
		fixture.Max.Verify(x => x.EditMessageAsync("card-1", It.Is<string>(text => text.Contains("Напоминание выключено")),
			It.Is<IReadOnlyList<MaxAttachment>>(attachments => attachments.OfType<InlineKeyboardAttachment>().Any(keyboard =>
				keyboard.Payload.Buttons.SelectMany(row => row).OfType<CallbackButton>().Any(button => button.Payload == "remind:" + item.Id))),
			It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Empty_recommendations_without_saved_events_offer_interests_not_empty_saved_list()
	{
		var fixture = new Fixture();
		fixture.User.HasCompletedOnboarding = true;
		fixture.Recommendations.Setup(x => x.GetTopEventsAsync(fixture.User.Id, 3, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.FindEvents), CancellationToken.None);
		var call = Assert.Single(fixture.Max.Invocations, call => call.Method.Name == nameof(IMaxBotClient.SendMessageToUserAsync));
		var keyboard = Assert.IsType<InlineKeyboardAttachment>(Assert.Single((IReadOnlyList<MaxAttachment>)call.Arguments[2]));
		var payloads = keyboard.Payload.Buttons.SelectMany(row => row).OfType<CallbackButton>().Select(button => button.Payload).ToList();
		Assert.Contains("menu:interests", payloads);
		Assert.DoesNotContain("menu:saved", payloads);
	}

	[Fact]
	public async Task Past_saved_list_does_not_offer_future_events()
	{
		var fixture = new Fixture();
		fixture.Saved.Setup(x => x.GetSavedEventsAsync(fixture.User.Id, It.IsAny<CancellationToken>())).ReturnsAsync([
			new Event { Title = "Будущее", EventDateTime = DateTime.UtcNow.AddDays(2) },
			new Event { Title = "Прошедшее", EventDateTime = DateTime.UtcNow.AddDays(-2) }]);
		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.SavedEvents, Past: true), CancellationToken.None);
		var messages = fixture.Max.Invocations.Where(call => call.Method.Name == nameof(IMaxBotClient.SendMessageToUserAsync)).Select(call => (string)call.Arguments[1]).ToList();
		Assert.Contains(messages, text => text.Contains("Прошедшее"));
		Assert.DoesNotContain(messages, text => text.Contains("Будущее"));
	}

	[Fact]
	public async Task Last_interest_cannot_be_removed_from_completed_profile()
	{
		var fixture = new Fixture();
		var tag = new Tag { Id = Guid.NewGuid(), Name = "Наука" };
		fixture.Tags.Setup(x => x.GetTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([tag]);
		fixture.Users.Setup(x => x.GetUserTagIdsAsync(fixture.User.Id, It.IsAny<CancellationToken>())).ReturnsAsync([tag.Id]);
		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.SetInterest, tag.Id, false, MessageId: "interests-1"), CancellationToken.None);
		Assert.DoesNotContain(fixture.Users.Invocations, call => call.Method.Name == nameof(IUserService.UpdateUserTagsAsync));
		fixture.Max.Verify(x => x.EditMessageAsync("interests-1", It.Is<string>(text => text.Contains("Оставь хотя бы один")),
			It.IsAny<IReadOnlyList<MaxAttachment>>(), It.IsAny<CancellationToken>()), Times.Once);
	}

	[Fact]
	public async Task Started_event_is_not_silently_retried()
	{
		var fixture = new Fixture();
		var item = new Event { Id = Guid.NewGuid(), EventDateTime = DateTime.UtcNow.AddHours(-1) };
		fixture.Events.Setup(x => x.GetEventByIdAsync(item.Id, It.IsAny<CancellationToken>(), EventStatus.Published)).ReturnsAsync(item);
		fixture.Saved.Setup(x => x.SaveEventAsync(fixture.User.Id, item.Id, It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("Можно сохранить только опубликованное предстоящее мероприятие."));
		await fixture.Scenario.HandleAsync(new BotCommand(42, BotCommandType.Remind, item.Id, MessageId: "card-1"), CancellationToken.None);
		fixture.Max.Verify(x => x.EditMessageAsync("card-1", It.Is<string>(text => text.Contains("Напоминание не включено")),
			It.IsAny<IReadOnlyList<MaxAttachment>>(), It.IsAny<CancellationToken>()), Times.Once);
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
			Saved.Setup(x => x.GetSavedEventsAsync(User.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
			Users.Setup(x => x.GetUserTagIdsAsync(User.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
			Tags.Setup(x => x.GetTagsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
			Users.Setup(x => x.CreateByMaxUserIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(User);
			Max.Setup(x => x.SendMessageToUserAsync(It.IsAny<long>(), It.IsAny<string?>(),
				It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, false, It.IsAny<CancellationToken>()))
				.ReturnsAsync(new MaxMessage());
			Scenario = new BotScenario(Users.Object, Tags.Object, Recommendations.Object, Saved.Object,
				Events.Object, Max.Object, Options.Create(new MaxOptions()));
		}
	}
}
