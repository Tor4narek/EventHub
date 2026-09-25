namespace ImageStorage;

public sealed class MinioOptions
{
	public string Endpoint { get; set; } = null!;
	public string AccessKey { get; set; } = null!;
	public string SecretKey { get; set; } = null!;
	public string BucketName { get; set; } = null!;
	public bool UseSsl { get; set; }
	public string PublicBaseUrl { get; set; } = null!;
}
