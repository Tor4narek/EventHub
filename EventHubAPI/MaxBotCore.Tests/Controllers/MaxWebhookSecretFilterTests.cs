using MaxBotCore.Configuration;
using MaxBotCore.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MaxBotCore.Tests.Controllers;

public sealed class MaxWebhookSecretFilterTests
{
	[Fact]
	public async Task Matching_secret_allows_webhook()
	{
		var context = CreateContext("secret");
		var filter = CreateFilter();
		var invoked = false;

		await filter.OnActionExecutionAsync(context, () =>
		{
			invoked = true;
			return Task.FromResult(new ActionExecutedContext(
				new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor), [], context.Controller));
		});

		Assert.True(invoked);
		Assert.Null(context.Result);
	}

	[Fact]
	public async Task Wrong_secret_rejects_webhook()
	{
		var context = CreateContext("wrong");
		var filter = CreateFilter();
		var invoked = false;

		await filter.OnActionExecutionAsync(context, () =>
		{
			invoked = true;
			return Task.FromResult(new ActionExecutedContext(
				new ActionContext(context.HttpContext, context.RouteData, context.ActionDescriptor), [], context.Controller));
		});

		Assert.False(invoked);
		Assert.IsType<UnauthorizedResult>(context.Result);
	}

	private static MaxWebhookSecretFilter CreateFilter() =>
		new(Options.Create(new MaxOptions { WebhookSecret = "secret" }),
			NullLogger<MaxWebhookSecretFilter>.Instance);

	private static ActionExecutingContext CreateContext(string provided)
	{
		var http = new DefaultHttpContext();
		http.Request.Headers["X-Max-Bot-Api-Secret"] = provided;
		return new ActionExecutingContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), [],
			new Dictionary<string, object?>(), new object());
	}
}
