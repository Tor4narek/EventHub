using ImageStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Minio.Exceptions;

namespace EventHubAPI.Controllers;

[ApiController]
public sealed class ImagesController : ControllerBase
{
	private readonly IImageStorageService _images;

	public ImagesController(IImageStorageService images)
	{
		_images = images;
	}

	[HttpPost("api/admin/images")]
	[Authorize(Roles = "Admin")]
	[Consumes("multipart/form-data")]
	[RequestSizeLimit(MinioImageStorageService.MaxImageSize + 1024 * 1024)]
	public async Task<ActionResult<StoredImage>> Upload([FromForm] IFormFile file,
		CancellationToken cancellationToken)
	{
		if (file is null)
		{
			return BadRequest("Выберите изображение.");
		}
		await using var content = file.OpenReadStream();
		return Ok(await _images.UploadAsync(content, file.Length, file.ContentType, cancellationToken));
	}

	[HttpGet("api/media/{**objectKey}")]
	[AllowAnonymous]
	public async Task<IActionResult> Download(string objectKey, CancellationToken cancellationToken)
	{
		var contentType = _images.GetContentType(objectKey);
		try
		{
			Response.ContentType = contentType;
			await _images.DownloadAsync(objectKey, Response.Body, cancellationToken);
			return new EmptyResult();
		}
		catch (ObjectNotFoundException)
		{
			return NotFound();
		}
	}
}
