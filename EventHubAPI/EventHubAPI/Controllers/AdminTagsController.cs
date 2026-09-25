using EventHubAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace EventHubAPI.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin/tags")]
public class AdminTagsController : ControllerBase
{
	private readonly ITagService _tags;

	public AdminTagsController(ITagService tags)
	{
		_tags = tags;
	}

	[HttpPost]
	public async Task<ActionResult<TagResponse>> Create(TagRequest request, CancellationToken cancellationToken)
	{
		var tag = await _tags.CreateTagAsync(request.Name, request.Description, request.Examples, cancellationToken);
		return Created($"/api/tags/{tag.Id}", TagResponse.From(tag));
	}

	[HttpPut("{tagId:guid}")]
	public async Task<ActionResult<TagResponse>> Update(Guid tagId, TagRequest request, CancellationToken cancellationToken)
	{
		var tag = await _tags.UpdateTagAsync(tagId, request.Name, request.Description, request.Examples, cancellationToken);
		return Ok(TagResponse.From(tag));
	}

	[HttpDelete("{tagId:guid}")]
	public async Task<IActionResult> Delete(Guid tagId, CancellationToken cancellationToken)
	{
		await _tags.DeleteTagAsync(tagId, cancellationToken);
		return NoContent();
	}
}
