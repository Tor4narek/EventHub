namespace ImageStorage;

public interface IImageStorageService
{
	Task<StoredImage> UploadAsync(Stream content, long length, string contentType,
		CancellationToken cancellationToken = default);

	Task DownloadAsync(string objectKey, Stream destination,
		CancellationToken cancellationToken = default);

	string GetContentType(string objectKey);
}

public record StoredImage(string ObjectKey, string Url);
