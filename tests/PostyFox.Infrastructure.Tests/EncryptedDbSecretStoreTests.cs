using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using PostyFox.Infrastructure.Secrets;
using PostyFox.Infrastructure.Tests.Support;
using Xunit;

namespace PostyFox.Infrastructure.Tests;

public class EncryptedDbSecretStoreTests
{
    private static EncryptedDbSecretStore New(SqliteDb db)
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new EncryptedDbSecretStore(db.Context, Options.Create(new SecretStoreOptions { EncryptionKey = key }));
    }

    [Fact]
    public async Task Set_get_roundtrips_and_stores_ciphertext()
    {
        using var db = new SqliteDb();
        var store = New(db);

        await store.SetSecretAsync("k", "plaintext-value");

        Assert.Equal("plaintext-value", await store.GetSecretAsync("k"));
        var row = Assert.Single(db.Context.Secrets);
        Assert.DoesNotContain("plaintext-value", row.CipherText); // encrypted at rest
    }

    [Fact]
    public async Task Overwrite_updates_value()
    {
        using var db = new SqliteDb();
        var store = New(db);
        await store.SetSecretAsync("k", "one");
        await store.SetSecretAsync("k", "two");
        Assert.Equal("two", await store.GetSecretAsync("k"));
        Assert.Single(db.Context.Secrets);
    }

    [Fact]
    public async Task Delete_removes_secret()
    {
        using var db = new SqliteDb();
        var store = New(db);
        await store.SetSecretAsync("k", "v");
        await store.DeleteSecretAsync("k");
        Assert.Null(await store.GetSecretAsync("k"));
    }

    [Fact]
    public async Task Missing_secret_returns_null()
    {
        using var db = new SqliteDb();
        Assert.Null(await New(db).GetSecretAsync("nope"));
    }
}
