using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EventHubAPI.Auth;
using EventHubAPI.Controllers;
using EventHubAPI.Models;
using MaxBotCore.Client;
using MaxBotCore.Configuration;
using MaxBotCore.Contracts.Attachments;
using MaxBotCore.Contracts.Messages;
using MaxBotCore.Scenarios;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Services.Interfaces;
using Storage.Entities;
using Xunit;

namespace MaxBotCore.Tests.Scenarios;

public sealed class WebAppIntegrationTests
{
    [Fact]
    public async Task Max_login_bot_and_profile_endpoints_share_the_same_user()
    {
        const string botToken = "test-bot-token";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Max:BotToken"] = botToken,
            ["Jwt:SigningKey"] = new string('x', 64)
        }).Build();
        var validator = new MaxInitDataValidator(configuration);
        var initData = SignedInitData(botToken, 42);
        Assert.Equal(42L, validator.Validate(initData));
        Assert.Null(validator.Validate(initData.Replace("42", "43")));

        var user = new User { Id = Guid.NewGuid(), MaxUserId = 42, HasCompletedOnboarding = true };
        var users = new Mock<IUserService>();
        users.Setup(x => x.CreateByMaxUserIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        users.Setup(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        var interests = new List<Guid>();
        users.Setup(x => x.GetUserTagIdsAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(() => interests);
        users.Setup(x => x.UpdateUserTagsAsync(user.Id, It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlyCollection<Guid>, CancellationToken>((_, ids, _) => interests = ids.ToList())
            .Returns(Task.CompletedTask);
        users.Setup(x => x.SetWeeklyDigestAsync(user.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, bool, CancellationToken>((_, enabled, _) => user.IsWeeklyDigestEnabled = enabled)
            .Returns(Task.CompletedTask);

        var login = await new AuthController().LoginWithMax(new MaxLoginRequest(initData), validator, users.Object,
            new TokenService(configuration), CancellationToken.None);
        var session = Assert.IsType<TokenResponse>(Assert.IsType<OkObjectResult>(login.Result).Value);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.AccessToken);
        Assert.Equal(user.Id.ToString(), jwt.Subject);

        var item = new Event { Id = Guid.NewGuid(), Title = "Лекция", Description = "Описание", Location = "Онлайн",
            Source = "https://example.com", EventDateTime = DateTime.UtcNow.AddDays(2), EventStatus = EventStatus.Published };
        var savedItems = new List<Event>();
        var saved = new Mock<IUserEventService>();
        saved.Setup(x => x.SaveEventAsync(user.Id, item.Id, It.IsAny<CancellationToken>()))
            .Callback(() => { if (!savedItems.Any(e => e.Id == item.Id)) savedItems.Add(item); }).Returns(Task.CompletedTask);
        saved.Setup(x => x.RemoveEventAsync(user.Id, item.Id, It.IsAny<CancellationToken>()))
            .Callback(() => savedItems.Clear()).Returns(Task.CompletedTask);
        saved.Setup(x => x.GetSavedEventsAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(() => savedItems.ToList());
        var events = new Mock<IEventService>();
        events.Setup(x => x.GetEventByIdAsync(item.Id, It.IsAny<CancellationToken>(), EventStatus.Published)).ReturnsAsync(item);
        var recommendations = new Mock<IRecomendationService>();
        recommendations.Setup(x => x.GetTopEventsAsync(user.Id, 3, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([item]);
        var max = new Mock<IMaxBotClient>();
        max.Setup(x => x.SendMessageToUserAsync(42, It.IsAny<string?>(), It.IsAny<IReadOnlyList<MaxAttachment>?>(),
            null, true, It.IsAny<CancellationToken>())).ReturnsAsync(new MaxMessage());
        var scenario = new BotScenario(users.Object, new Mock<ITagService>().Object, recommendations.Object,
            saved.Object, events.Object, max.Object, Options.Create(new MaxOptions()));
        var me = new MeController(users.Object, saved.Object, recommendations.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext
            { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, jwt.Subject)], "test")) } }
        };

        await scenario.HandleAsync(new BotCommand(42, BotCommandType.Remind, item.Id), CancellationToken.None);
        var webSaved = await me.GetSaved(CancellationToken.None);
        var response = Assert.IsAssignableFrom<IReadOnlyList<EventResponse>>(Assert.IsType<OkObjectResult>(webSaved.Result).Value);
        Assert.Equal(item.Id, Assert.Single(response).Id);
        await me.Remove(item.Id, CancellationToken.None);
        Assert.Empty(savedItems);
        await me.Save(item.Id, CancellationToken.None);
        await scenario.HandleAsync(new BotCommand(42, BotCommandType.SavedEvents), CancellationToken.None);
        max.Verify(x => x.SendMessageToUserAsync(42, It.Is<string?>(text => text!.Contains("Лекция")),
            It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, true, It.IsAny<CancellationToken>()), Times.Once);

        var tagId = Guid.NewGuid();
        await me.UpdateInterests(new TagIdsRequest([tagId]), CancellationToken.None);
        Assert.Equal(tagId, Assert.Single(await users.Object.GetUserTagIdsAsync(user.Id, CancellationToken.None)));
        await me.UpdateSettings(new SettingsRequest(false), CancellationToken.None);
        await scenario.HandleAsync(new BotCommand(42, BotCommandType.Settings), CancellationToken.None);
        max.Verify(x => x.SendMessageToUserAsync(42, "Еженедельная подборка: выключена",
            It.IsAny<IReadOnlyList<MaxAttachment>?>(), null, true, It.IsAny<CancellationToken>()), Times.Once);
        await me.GetRecommendations(3, CancellationToken.None);
        recommendations.VerifyAll();
        await me.Get(CancellationToken.None);
        users.Verify(x => x.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static string SignedInitData(string botToken, long maxUserId)
    {
        var fields = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["auth_date"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            ["user"] = $"{{\"id\":{maxUserId}}}"
        };
        var secret = HMACSHA256.HashData(Encoding.UTF8.GetBytes("WebAppData"), Encoding.UTF8.GetBytes(botToken));
        var hash = HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(string.Join("\n", fields.Select(f => $"{f.Key}={f.Value}"))));
        fields["hash"] = Convert.ToHexString(hash);
        return string.Join("&", fields.Select(f => $"{f.Key}={Uri.EscapeDataString(f.Value)}"));
    }
}
