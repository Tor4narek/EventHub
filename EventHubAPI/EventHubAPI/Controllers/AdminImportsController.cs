using EventHubAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Dto;
using Services.Interfaces;
using Storage.Entities;

namespace EventHubAPI.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/imports")]
public class AdminImportsController(IEventImportService importer) : ControllerBase
{
	[HttpPost]
	[ProducesResponseType(typeof(EventImportStartResponse), StatusCodes.Status202Accepted)]
	public async Task<IActionResult> Start(EventImportRequest request, CancellationToken cancellationToken)
	{
		var importId = await importer.StartImportAsync(request.Urls, cancellationToken);
		return AcceptedAtAction(nameof(Get), new { importId }, new EventImportStartResponse(importId));
	}

	[HttpGet]
	[ProducesResponseType(typeof(IReadOnlyCollection<EventImportRun>), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetAll(CancellationToken cancellationToken) => Ok(await importer.GetImportsAsync(cancellationToken));

	[HttpGet("{importId:guid}")]
	[ProducesResponseType(typeof(EventImportRun), StatusCodes.Status200OK)]
	public async Task<IActionResult> Get(Guid importId, CancellationToken cancellationToken) => Ok(await importer.GetImportAsync(importId, cancellationToken));

	[HttpGet("{importId:guid}/items/{itemId:guid}")]
	[ProducesResponseType(typeof(EventImportItem), StatusCodes.Status200OK)]
	public async Task<IActionResult> GetItem(Guid importId, Guid itemId, CancellationToken cancellationToken) => Ok(await importer.GetItemAsync(importId, itemId, cancellationToken));

	[HttpPut("{importId:guid}/items/{itemId:guid}")]
	[ProducesResponseType(typeof(EventImportItem), StatusCodes.Status200OK)]
	public async Task<IActionResult> UpdateItem(Guid importId, Guid itemId, EventImportEditDto request, CancellationToken cancellationToken) => Ok(await importer.UpdateItemAsync(importId, itemId, request, cancellationToken));

	[HttpPost("{importId:guid}/items/{itemId:guid}/confirm")]
	[ProducesResponseType(typeof(EventImportConfirmResponse), StatusCodes.Status200OK)]
	public async Task<IActionResult> Confirm(Guid importId, Guid itemId, CancellationToken cancellationToken) => Ok(new EventImportConfirmResponse(await importer.ConfirmItemAsync(importId, itemId, cancellationToken)));

	[HttpPost("{importId:guid}/items/{itemId:guid}/retry")]
	[ProducesResponseType(StatusCodes.Status202Accepted)]
	public async Task<IActionResult> Retry(Guid importId, Guid itemId, CancellationToken cancellationToken)
	{
		await importer.RetryItemAsync(importId, itemId, cancellationToken);
		return Accepted();
	}
}
