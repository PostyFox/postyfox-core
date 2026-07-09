using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PostyFox.Infrastructure.Persistence;

namespace PostyFox.Infrastructure.Tests.Support;

/// <summary>Creates an isolated in-memory SQLite-backed AppDbContext for repository tests.</summary>
public sealed class SqliteDb : IDisposable
{
    private readonly SqliteConnection _connection;
    public AppDbContext Context { get; }

    public SqliteDb()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        Context = new AppDbContext(options);
        Context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
