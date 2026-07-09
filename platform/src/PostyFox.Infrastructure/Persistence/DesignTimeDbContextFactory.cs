using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PostyFox.Infrastructure.Persistence;

/// <summary>Enables `dotnet ef migrations` without booting a host.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__Postgres")
                   ?? "Host=localhost;Port=5432;Database=postyfox;Username=postyfox;Password=postyfox";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(conn, o => o.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName))
            .Options;
        return new AppDbContext(options);
    }
}
