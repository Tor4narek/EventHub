using System.Text.Json;
using FluentAssertions;
using MaxBotCore.Contracts.Serialization;
using MaxBotCore.Contracts.Updates;
using Xunit;

namespace MaxBotCore.Tests.Contracts;

/// <summary>
/// Проверяет что полиморфная десериализация Update по полю update_type
/// работает корректно и возвращает правильные подтипы.
/// </summary>
public sealed class UpdateDeserializationTests
{
	private static readonly JsonSerializerOptions Json = MaxJsonSerializerOptions.Default;

	[Fact]
	public void MessageCreated_deserializes_when_update_type_is_last()
	{
		const string json = """
		{
			"timestamp": 1737500130100,
			"message": { "sender": { "user_id": 42, "is_bot": false }, "body": { "text": "Привет" } },
			"update_type": "message_created"
		}
		""";

		var update = JsonSerializer.Deserialize<MaxUpdate>(json, Json);

		update.Should().BeOfType<MessageCreatedUpdate>();
		((MessageCreatedUpdate)update!).Message.Sender!.UserId.Should().Be(42);
	}

	[Fact]
	public void MessageCreated_deserializes_to_MessageCreatedUpdate()
	{
		const string json = """
		{
			"update_type": "message_created",
			"timestamp": 1737500130100,
			"message": {
				"sender": { "user_id": 42, "first_name": "Иван", "is_bot": false },
				"recipient": { "user_id": 100, "chat_type": "dialog" },
				"timestamp": 1737500130100,
				"body": { "mid": "abc", "seq": 1, "text": "Привет" }
			}
		}
		""";

		var update = JsonSerializer.Deserialize<MaxUpdate>(json, Json);

		update.Should().BeOfType<MessageCreatedUpdate>();
		var msg = (MessageCreatedUpdate)update!;
		msg.UpdateType.Should().Be("message_created");
		msg.Message.Body!.Text.Should().Be("Привет");
		msg.Message.Sender!.UserId.Should().Be(42);
		msg.Message.Sender.FirstName.Should().Be("Иван");
	}

	[Fact]
	public void MessageCallback_deserializes_to_MessageCallbackUpdate()
	{
		const string json = """
		{
			"update_type": "message_callback",
			"timestamp": 1737500130100,
			"callback": {
				"timestamp": 1737500130100,
				"callback_id": "cb-1",
				"payload": "onboarding:tag:abc-123",
				"user": { "user_id": 42, "first_name": "Иван", "is_bot": false }
			}
		}
		""";

		var update = JsonSerializer.Deserialize<MaxUpdate>(json, Json);

		update.Should().BeOfType<MessageCallbackUpdate>();
		var cb = (MessageCallbackUpdate)update!;
		cb.Callback.Payload.Should().Be("onboarding:tag:abc-123");
		cb.Callback.User!.UserId.Should().Be(42);
	}

	[Fact]
	public void BotStarted_deserializes_to_BotStartedUpdate()
	{
		const string json = """
		{
			"update_type": "bot_started",
			"timestamp": 1737500130100,
			"chat_id": 999,
			"user": { "user_id": 42, "first_name": "Иван", "is_bot": false },
			"user_locale": "ru"
		}
		""";

		var update = JsonSerializer.Deserialize<MaxUpdate>(json, Json);

		update.Should().BeOfType<BotStartedUpdate>();
		var started = (BotStartedUpdate)update!;
		started.ChatId.Should().Be(999);
		started.User.UserId.Should().Be(42);
		started.UserLocale.Should().Be("ru");
	}

	[Fact]
	public void Unknown_update_type_falls_back_to_base_MaxUpdate()
	{
		// Если MAX добавит новый тип события — не должны падать.
		// Десериализатор вернёт объект базового MaxUpdate с UpdateType = "unknown".
		const string json = """
		                    {
		                    	"update_type": "dialog_cleared",
		                    	"timestamp": 1737500130100,
		                    	"chat_id": 999
		                    }
		                    """;

		var update = JsonSerializer.Deserialize<MaxUpdate>(json, Json);

		update.Should().NotBeNull();
		// Именно базовый тип, а не какой-то из наследников
		update!.GetType().Should().Be(typeof(MaxUpdate));
		update.Timestamp.Should().Be(1737500130100);
		update.UpdateType.Should().Be("unknown");
	}
}
