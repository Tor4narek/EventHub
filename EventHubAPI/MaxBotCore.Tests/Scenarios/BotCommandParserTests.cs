using MaxBotCore.Scenarios;
using MaxBotCore.Contracts.Attachments.Buttons;
using Storage.Entities;
using Xunit;

namespace MaxBotCore.Tests.Scenarios;

public sealed class BotCommandParserTests
{
	[Theory]
	[InlineData("/menu", BotCommandType.Menu)]
	[InlineData("/help", BotCommandType.Help)]
	[InlineData("/saved", BotCommandType.SavedEvents)]
	[InlineData("/settings", BotCommandType.Settings)]
	[InlineData("/interests", BotCommandType.Interests)]
	public void Parses_quick_commands(string text, BotCommandType type) =>
		Assert.Equal(type, BotCommandParser.ParseText(42, text).Type);

	[Fact]
	public void Home_does_not_restart_welcome() =>
		Assert.Equal(BotCommandType.Menu, BotCommandParser.ParseCallback(42, "menu:home").Type);

	[Fact]
	public void Parses_past_saved_page()
	{
		var command = BotCommandParser.ParseCallback(42, "saved:past:5");
		Assert.True(command.Past);
		Assert.Equal(5, command.Offset);
	}

	[Fact]
	public void Closed_registration_links_to_organizer_without_promising_registration()
	{
		var card = BotMessageFactory.EventCard(new Event { EventDateTime = DateTime.UtcNow.AddDays(2), Deadline = DateTime.UtcNow.AddDays(-1), Source = "https://example.com" });
		Assert.Contains("Срок регистрации закончился", card.Text);
		var keyboard = Assert.IsType<MaxBotCore.Contracts.Attachments.InlineKeyboardAttachment>(Assert.Single(card.Attachments));
		Assert.StartsWith("Сайт организатора", Assert.IsType<LinkButton>(keyboard.Payload.Buttons[0][0]).Text);
	}
	[Fact]
	public void Parses_reminder_with_event_id()
	{
		var id = Guid.NewGuid();
		var command = BotCommandParser.ParseCallback(42, $"remind:{id}");
		Assert.Equal(BotCommandType.Remind, command.Type);
		Assert.Equal(id, command.EntityId);
	}

	[Fact]
	public void Parses_explicit_interest_action_without_toggle()
	{
		var id = Guid.NewGuid();
		var command = BotCommandParser.ParseCallback(42, $"interest:on:add:{id}");
		Assert.Equal(BotCommandType.SetInterest, command.Type);
		Assert.True(command.Enabled);
		Assert.True(command.IsOnboarding);
	}

	[Theory]
	[InlineData("remind:invalid")]
	[InlineData("interest:on:add:00000000-0000-0000-0000-000000000000")]
	[InlineData("unknown:payload")]
	public void Unknown_payload_is_ignored(string payload) =>
		Assert.Equal(BotCommandType.Unknown, BotCommandParser.ParseCallback(42, payload).Type);

	[Fact]
	public void Onboarding_keeps_done_button_after_first_tag()
	{
		var tag = new Tag { Id = Guid.NewGuid(), Name = "Наука", Description = "", Examples = [] };
		var keyboard = BotMessageFactory.Interests([tag], [tag.Id], true);
		Assert.Equal("onboarding:done", Assert.IsType<CallbackButton>(keyboard.Payload.Buttons.Last()[0]).Payload);
		Assert.Equal($"interest:on:remove:{tag.Id}",
			Assert.IsType<CallbackButton>(keyboard.Payload.Buttons[0][0]).Payload);
	}

	[Fact]
	public void Parses_saved_page_offset()
	{
		var command = BotCommandParser.ParseCallback(42, "saved:5");
		Assert.Equal(BotCommandType.SavedEvents, command.Type);
		Assert.Equal(5, command.Offset);
	}
}
