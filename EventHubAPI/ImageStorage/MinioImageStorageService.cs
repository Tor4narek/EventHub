using Minio;
using Minio.DataModel.Args;
using Microsoft.Extensions.Options;

namespace ImageStorage;

public sealed class MinioImageStorageService : IImageStorageService
{
	public const long MaxImageSize = 10 * 1024 * 1024;

	private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
	{
		["image/jpeg"] = ".jpg",
		["image/png"] = ".png",
		["image/webp"] = ".webp",
		["image/gif"] = ".gif"
	};

	private readonly IMinioClient _minioClient;
	private readonly MinioOptions _options;
	private readonly SemaphoreSlim _bucketLock = new(1, 1);
	private bool _bucketReady;

	public MinioImageStorageService(IMinioClient minioClient, IOptions<MinioOptions> options)
	{
		_minioClient = minioClient;
		_options = options.Value;
	}

	public async Task<StoredImage> UploadAsync(Stream content, long length, string contentType,
		CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(content);
		if (length is < 1 or > MaxImageSize)
		{
			throw new ArgumentException($"Размер изображения должен быть от 1 байта до {MaxImageSize} байт.");
		}
		if (!Extensions.TryGetValue(contentType, out var extension))
		{
			throw new ArgumentException("Допустимые форматы: JPEG, PNG, WebP и GIF.", nameof(contentType));
		}

		var objectKey = $"images/{DateTime.UtcNow:yyyy/MM/dd}/{Guid.NewGuid():N}{extension}";
		await EnsureBucketAsync(cancellationToken);
		await _minioClient.PutObjectAsync(new PutObjectArgs()
			.WithBucket(_options.BucketName)
			.WithObject(objectKey)
			.WithStreamData(content)
			.WithObjectSize(length)
			.WithContentType(contentType), cancellationToken);

		var url = $"{_options.PublicBaseUrl.TrimEnd('/')}/api/media/{objectKey}";
		return new StoredImage(objectKey, url);
	}

	private async Task EnsureBucketAsync(CancellationToken cancellationToken)
	{
		if (_bucketReady)
		{
			return;
		}
		await _bucketLock.WaitAsync(cancellationToken);
		try
		{
			if (_bucketReady)
			{
				return;
			}
			var exists = await _minioClient.BucketExistsAsync(
				new BucketExistsArgs().WithBucket(_options.BucketName), cancellationToken);
			if (!exists)
			{
				await _minioClient.MakeBucketAsync(
					new MakeBucketArgs().WithBucket(_options.BucketName), cancellationToken);
			}
			_bucketReady = true;
		}
		finally
		{
			_bucketLock.Release();
		}
	}

	public async Task DownloadAsync(string objectKey, Stream destination,
		CancellationToken cancellationToken = default)
	{
		GetContentType(objectKey);
		await _minioClient.GetObjectAsync(new GetObjectArgs()
			.WithBucket(_options.BucketName)
			.WithObject(objectKey)
			.WithCallbackStream((stream, token) => stream.CopyToAsync(destination, token)), cancellationToken);
	}

	public string GetContentType(string objectKey)
	{
		if (string.IsNullOrWhiteSpace(objectKey) || !objectKey.StartsWith("images/", StringComparison.Ordinal) ||
			objectKey.Contains("..", StringComparison.Ordinal))
		{
			throw new ArgumentException("Некорректный ключ изображения.", nameof(objectKey));
		}

		var extension = Path.GetExtension(objectKey);
		return Extensions.FirstOrDefault(pair => pair.Value == extension).Key
		       ?? throw new ArgumentException("Неподдерживаемый формат изображения.", nameof(objectKey));
	}
}
