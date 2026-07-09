using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using PostyFox.Application.Abstractions;

namespace PostyFox.Infrastructure.Storage;

/// <summary>S3-compatible object store (works against MinIO, AWS S3, GCS, Azure Blob via S3 API).</summary>
public sealed class S3ObjectStore : IObjectStore, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;
    private int _bucketReady;

    public S3ObjectStore(IOptions<S3Options> options)
    {
        var o = options.Value;
        _bucket = o.Bucket;
        var config = new AmazonS3Config { ServiceURL = o.ServiceUrl, ForcePathStyle = o.ForcePathStyle };
        _client = new AmazonS3Client(o.AccessKey, o.SecretKey, config);
    }

    private static string ObjectKey(string container, string key) => $"{container}/{key}";

    private async Task EnsureBucketAsync(CancellationToken ct)
    {
        if (Interlocked.CompareExchange(ref _bucketReady, 1, 0) == 1) return;
        try { await _client.PutBucketAsync(new PutBucketRequest { BucketName = _bucket }, ct); }
        catch (AmazonS3Exception ex) when (ex.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists") { }
    }

    public async Task PutAsync(string container, string key, Stream content, string contentType, CancellationToken ct = default)
    {
        await EnsureBucketAsync(ct);
        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = ObjectKey(container, key),
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        }, ct);
    }

    public Task PutTextAsync(string container, string key, string content, string contentType = "text/plain", CancellationToken ct = default)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return PutAsync(container, key, stream, contentType, ct);
    }

    public async Task<Stream> GetAsync(string container, string key, CancellationToken ct = default)
    {
        var response = await _client.GetObjectAsync(_bucket, ObjectKey(container, key), ct);
        var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms, ct);
        ms.Position = 0;
        return ms;
    }

    public async Task<string> GetTextAsync(string container, string key, CancellationToken ct = default)
    {
        await using var stream = await GetAsync(container, key, ct);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(ct);
    }

    public async Task<bool> ExistsAsync(string container, string key, CancellationToken ct = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(_bucket, ObjectKey(container, key), ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public Task DeleteAsync(string container, string key, CancellationToken ct = default) =>
        _client.DeleteObjectAsync(_bucket, ObjectKey(container, key), ct);

    public void Dispose() => _client.Dispose();
}
