using Minio;
using Minio.DataModel.Args;
using ReviewInsights.Api.Configuration;

namespace ReviewInsights.Api.Infrastructure;

public class MinioFileStorageService : IFileStorageService
{
    private readonly IMinioClient _client;
    private readonly string _bucketName;

    public MinioFileStorageService(MinioSettings settings)
    {
        _bucketName = settings.BucketName;
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
            await _client.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_bucketName), ct);
        }
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType,
        CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(ct);

        var fileKey = $"uploads/{Guid.NewGuid()}/{fileName}";

        await _client.PutObjectAsync(new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(fileKey)
            .WithStreamData(fileStream)
            .WithObjectSize(fileStream.Length)
            .WithContentType(contentType), ct);

        return fileKey;
    }

    public async Task<Stream> DownloadFileAsync(string fileKey, CancellationToken ct = default)
    {
        var memoryStream = new MemoryStream();

        await _client.GetObjectAsync(new GetObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(fileKey)
            .WithCallbackStream(stream => stream.CopyTo(memoryStream)), ct);

        memoryStream.Position = 0;
        return memoryStream;
    }

    public async Task DeleteFileAsync(string fileKey, CancellationToken ct = default)
    {
        await _client.RemoveObjectAsync(new RemoveObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(fileKey), ct);
    }
}
