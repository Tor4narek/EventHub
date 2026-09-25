using EventHubAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace EventHubAPI.Controllers;

[ApiController]
[Route("api/tags")]
public class TagsController : ControllerBase
{
	private readonly ITagService _tags;

	public TagsController(ITagService tags)
	{
		_tags = tags;
	}

	[HttpGet]
	public async Task<ActionResult<IReadOnlyList<TagResponse>>> GetAll(CancellationToken cancellationToken)
	{
		var tags = await _tags.GetTagsAsync(cancellationToken);
		return Ok(tags.Select(TagResponse.From).ToList());
	}

	[HttpGet("{tagId:guid}")]
	public async Task<ActionResult<TagResponse>> GetById(Guid tagId, CancellationToken cancellationToken)
	{
		return Ok(TagResponse.From(await _tags.GetTagAsync(tagId, cancellationToken)));
	}
}
