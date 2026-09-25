using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace EventHubAPI.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/imports")]
public class AdminImportsController : ControllerBase
{
	[HttpPost]
	public async Task<IActionResult> Start(
		[FromServices] IServiceProvider services,
		CancellationToken cancellationToken)
	{
		var importer = services.GetService<IEventImportService>();
		if (importer is null)
		{
			return StatusCode(StatusCodes.Status501NotImplemented, "Импортёр пока не подключён.");
		}

		var importId = await importer.StartImportAsync(cancellationToken);
		return Accepted(new { ImportId = importId });
	}
}
