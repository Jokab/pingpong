using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PingPong.Infrastructure.Persistence;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PingPongDbContext>
{
    public PingPongDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<PingPongDbContext>();

        // Default to SQLite for design-time unless overridden via env var
        var provider = Environment.GetEnvironmentVariable("PP_DB_PROVIDER") ?? "Sqlite";

        if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var cs = Environment.GetEnvironmentVariable("PP_CONNECTION_STRING")
                     ?? "Server=localhost;Database=PingPong;Trusted_Connection=True;TrustServerCertificate=True;";
            builder.UseSqlServer(cs);
        }
        else
        {
            var cs = Environment.GetEnvironmentVariable("PP_CONNECTION_STRING")
                     ?? "Data Source=pingpong_dev.db";
            builder.UseSqlite(cs);
        }

        return new PingPongDbContext(builder.Options);
    }
}
