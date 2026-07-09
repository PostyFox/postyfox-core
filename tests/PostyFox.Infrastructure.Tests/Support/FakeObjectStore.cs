using System.Text;
using PostyFox.Application.Abstractions;

namespace PostyFox.Infrastructure.Tests.Support;

public sealed class FakeObjectStore : IObjectStore
{
    public Dictionary<string, byte[]> Objects { get; } = new();
    private static string K(string c, string k) => $"{c}/{k}";

    public void Seed(string container, string key, byte[] data) => Objects[K(container, key)] = data;

    public Task PutAsync(string c, string k, Stream s, string ct, CancellationToken t = default)
    { using var ms = new MemoryStream(); s.CopyTo(ms); Objects[K(c, k)] = ms.ToArray(); return Task.CompletedTask; }
    public Task PutTextAsync(string c, string k, string content, string ct = "text/plain", CancellationToken t = default)
    { Objects[K(c, k)] = Encoding.UTF8.GetBytes(content); return Task.CompletedTask; }
    public Task<Stream> GetAsync(string c, string k, CancellationToken t = default)
        => Task.FromResult<Stream>(new MemoryStream(Objects.TryGetValue(K(c, k), out var b) ? b : []));
    public Task<string> GetTextAsync(string c, string k, CancellationToken t = default)
        => Task.FromResult(Objects.TryGetValue(K(c, k), out var b) ? Encoding.UTF8.GetString(b) : "");
    public Task<bool> ExistsAsync(string c, string k, CancellationToken t = default) => Task.FromResult(Objects.ContainsKey(K(c, k)));
    public Task DeleteAsync(string c, string k, CancellationToken t = default) { Objects.Remove(K(c, k)); return Task.CompletedTask; }
}
