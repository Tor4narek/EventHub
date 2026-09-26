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
	public async Task<IActionResult> Start(EventImportRequest request, CancellationToken cancellationToken)
	{
		var importId = await importer.StartImportAsync(request.Urls, cancellationToken);
		return AcceptedAtAction(nameof(Get), new { importId }, new { ImportId = importId });
	}

	[HttpGet]
	public async Task<IActionResult> GetAll(CancellationToken cancellationToken) => Ok(await importer.GetImportsAsync(cancellationToken));

	[HttpGet("{importId:guid}")]
	public async Task<IActionResult> Get(Guid importId, CancellationToken cancellationToken) => Ok(await importer.GetImportAsync(importId, cancellationToken));

	[HttpGet("{importId:guid}/items/{itemId:guid}")]
	public async Task<IActionResult> GetItem(Guid importId, Guid itemId, CancellationToken cancellationToken) => Ok(await importer.GetItemAsync(importId, itemId, cancellationToken));

	[HttpPut("{importId:guid}/items/{itemId:guid}")]
	public async Task<IActionResult> UpdateItem(Guid importId, Guid itemId, EventImportEditDto request, CancellationToken cancellationToken) => Ok(await importer.UpdateItemAsync(importId, itemId, request, cancellationToken));

	[HttpPost("{importId:guid}/items/{itemId:guid}/confirm")]
	public async Task<IActionResult> Confirm(Guid importId, Guid itemId, CancellationToken cancellationToken) => Ok(new { EventId = await importer.ConfirmItemAsync(importId, itemId, cancellationToken) });

	[HttpPost("{importId:guid}/items/{itemId:guid}/retry")]
	public async Task<IActionResult> Retry(Guid importId, Guid itemId, CancellationToken cancellationToken)
	{
		await importer.RetryItemAsync(importId, itemId, cancellationToken);
		return Accepted();
	}
}
