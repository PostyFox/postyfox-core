using PostyFox.Infrastructure.Connectors;
using PostyFox.Infrastructure.Tests.Support;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

/// <summary>
/// WTelegramClient's Session.Save() persists state via Position/Write/SetLength and never calls
/// Stream.Flush() — these tests guard against regressing to a Flush()-based persistence trigger,
/// which silently drops all Telegram session state (see BlobSessionStore remarks).
/// </summary>
public class BlobSessionStoreTests
{
    [Fact]
    public async Task Write_PersistsToObjectStoreImmediately_WithoutFlush()
    {
        var store = new FakeObjectStore();
        var session = await BlobSessionStore.OpenAsync(store, "user-1");

        var payload = new byte[] { 1, 2, 3, 4 };
        session.Write(payload, 0, payload.Length);
        // Deliberately do NOT call session.Flush() — WTelegramClient never does either.

        Assert.True(await store.ExistsAsync("telegram", "user-1"));
        var persisted = await store.GetTextAsync("telegram", "user-1");
        Assert.NotEmpty(persisted);
    }

    [Fact]
    public async Task SetLength_PersistsTruncatedState()
    {
        var store = new FakeObjectStore();
        var session = await BlobSessionStore.OpenAsync(store, "user-2");

        var payload = new byte[] { 1, 2, 3, 4, 5, 6 };
        session.Write(payload, 0, payload.Length);
        session.SetLength(2);

        using var persisted = await store.GetAsync("telegram", "user-2");
        using var ms = new MemoryStream();
        await persisted.CopyToAsync(ms);
        Assert.Equal(2, ms.Length);
    }

    [Fact]
    public async Task OpenAsync_LoadsExistingSessionBytes()
    {
        var store = new FakeObjectStore();
        store.Seed("telegram", "user-3", [9, 8, 7]);

        var session = await BlobSessionStore.OpenAsync(store, "user-3");

        Assert.Equal(3, session.Length);
        Assert.Equal(0, session.Position);
    }

    [Fact]
    public async Task Flush_IsANoOp_AndDoesNotThrow()
    {
        var store = new FakeObjectStore();
        var session = await BlobSessionStore.OpenAsync(store, "user-4");

        session.Flush();

        Assert.False(await store.ExistsAsync("telegram", "user-4"));
    }
}
