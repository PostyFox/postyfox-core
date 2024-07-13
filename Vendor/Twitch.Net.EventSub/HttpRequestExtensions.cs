using System.Text;
using Microsoft.AspNetCore.Http;

namespace Twitch.Net.EventSub;

public static class HttpRequestExtensions
{
    /// <summary>
    /// Retrieve the raw body as a string from the Request.Body stream
    /// </summary>
    public static async Task<string> GetRawBodyStringAsync(this HttpRequest request, Encoding? encoding = null)
    {
        encoding ??= Encoding.UTF8;

        using var reader = new StreamReader(request.Body, encoding);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Retrieves the raw body as a byte array from the Request.Body stream
    /// </summary>
    public static async Task<byte[]> GetRawBodyBytesAsync(this HttpRequest request)
    {
        await using var ms = new MemoryStream(2048);
        await request.Body.CopyToAsync(ms);
        return ms.ToArray();
    }
}