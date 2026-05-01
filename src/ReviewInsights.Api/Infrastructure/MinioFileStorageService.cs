using Minio;
using Minio.DataModel.Args;
using ReviewInsights.Api.Configuration;

namespace ReviewInsights.Api.Infrastructure;

public class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _client;
    private readonly string _bucketName;
    private readonly ILogger<MinioFileStorageService> _logger;

    public MinioFileStorageService(MinioSettings settings, ILogger<MinioFileStorageService> logger)
    {
        _bucketName = settings.BucketName;
        _logger = logger;
        _client = new MinioClient()
            .WithEndpoint(settings.Endpoint)
            .WithCredentials(settings.AccessKey, settings.SecretKey)
            .WithSSL(settings.UseSSL)
            .Build();
    }

    public async Task EnsureBucketExistsAsync(CancellationToken ct = default)
    {
        var found = await _client.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_bucketName), ct);

        if (!found)
        {
            _logger.LogInformation("Bucket {Bucket} not found, creating", _bucketName);
            await _client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_bucketName), ct);
            _logger.LogInformation("Bucket {Bucket} created successfully", _bucketName);
        }
        else
        {
            _logger.LogDebug("Bucket {Bucket} already exists", _bucketName);
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType,
        CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(ct);

        var fileKey = $"uploads/{Guid.NewGuid()}/{fileName}";

        _logger.LogInformation(
            "Uploading file {FileName} ({ContentType}, {Bytes} bytes) to bucket {Bucket} as {StorageKey}",
            fileName, contentType, fileStream.Length, _bucketName, fileKey);

        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(fileKey)
            .WithStreamData(fileStream)
            .WithObjectSize(fileStream.Length)
            .WithContentType(contentType), ct);

        _logger.LogInformation(
            "File {FileName} uploaded successfully as {StorageKey}", fileName, fileKey);

        return fileKey;
    }

    public async Task<Stream> DownloadFileAsync(string fileKey, CancellationToken ct = default)
    {
        _logger.LogDebug("Downloading file {StorageKey} from bucket {Bucket}", fileKey, _bucketName);

        var memoryStream = new MemoryStream();

        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(fileKey)
            .WithCallbackStream(stream => stream.CopyTo(memoryStream)), ct);

        memoryStream.Position = 0;

        _logger.LogDebug(
            "File {StorageKey} downloaded successfully ({Bytes} bytes)", fileKey, memoryStream.Length);

        return memoryStream;
    }

    public async Task DeleteFileAsync(string fileKey, CancellationToken ct = default)
    {
        _logger.LogInformation("Deleting file {StorageKey} from bucket {Bucket}", fileKey, _bucketName);

        await _client.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(fileKey), ct);

        _logger.LogInformation("File {StorageKey} deleted successfully", fileKey);
    }
}
