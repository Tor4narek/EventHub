using System.Text.Json;
using FluentAssertions;
using MaxBotCore.Contracts.Attachments;
using MaxBotCore.Contracts.Attachments.Buttons;
using MaxBotCore.Contracts.Requests;
using MaxBotCore.Contracts.Serialization;
using Xunit;

namespace MaxBotCore.Tests.Contracts;

/// <summary>
/// Проверяет корректность сериализации исходящих сообщений с inline-клавиатурой
/// в JSON, соответствующий контракту MAX API.
/// </summary>
public sealed class SendMessageSerializationTests
{
	private static readonly JsonSerializerOptions Json = MaxJsonSerializerOptions.Default;

	[Fact]
	public void SendMessageRequest_with_callback_button_serializes_correctly()
	{
		var request = new SendMessageRequest
		{
			Text = "Выбери действие",
			Attachments =
			[
				new InlineKeyboardAttachment
				{
					Payload = new InlineKeyboardPayload
					{
						Buttons =
						[
							[
								new CallbackButton { Text = "Сохранить", Payload = "event:save:123" },
								new CallbackButton { Text = "Убрать", Payload = "event:remove:123" }
							]
						]
					}
				}
			]
		};

		var json = JsonSerializer.Serialize(request, Json);

		// Проверяем ключевые куски JSON, чтобы не привязываться к порядку полей.
		json.Should().Contain("\"text\":\"Выбери действие\"");
		json.Should().Contain("\"type\":\"inline_keyboard\"");
		json.Should().Contain("\"type\":\"callback\"");
		json.Should().Contain("\"payload\":\"event:save:123\"");
		json.Should().Contain("\"payload\":\"event:remove:123\"");
	}

	[Fact]
	public void SendMessageRequest_without_optional_fields_omits_them()
	{
		// Если Notify, Format, Attachments не заданы — они не должны попадать в JSON.
		var request = new SendMessageRequest { Text = "Привет" };

		var json = JsonSerializer.Serialize(request, Json);

		json.Should().Contain("\"text\":\"Привет\"");
		json.Should().NotContain("\"notify\"");
		json.Should().NotContain("\"format\"");
		json.Should().NotContain("\"attachments\"");
	}

	[Fact]
	public void Buttons_serialize_polymorphically_by_type()
	{
		var buttons = new MaxButton[]
		{
			new CallbackButton { Text = "Cb", Payload = "p" },
			new LinkButton { Text = "Link", Url = "https://example.com" },
			new MessageButton { Text = "Msg" }
		};

		var json = JsonSerializer.Serialize(buttons, Json);

		json.Should().Contain("\"type\":\"callback\"");
		json.Should().Contain("\"type\":\"link\"");
		json.Should().Contain("\"type\":\"message\"");
		json.Should().Contain("\"url\":\"https://example.com\"");
	}
}