using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PingPong.Application.Interfaces;
using PingPong.Application.MatchSubmission;
using PingPong.Application.Shared;
using PingPong.Infrastructure.Persistence;
using PingPong.Infrastructure.Services;

namespace PingPong.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<PingPongDbContext>(
            (serviceProvider, options) =>
            {
                var env = serviceProvider.GetRequiredService<IHostEnvironment>();
                var isTesting = env.IsEnvironment("Testing");

                // In tests, prefer SQLite. If a shared SqliteConnection is registered, use it to keep the in-memory DB alive.
                if (isTesting)
                {
                    var sqliteConnection = serviceProvider.GetService<SqliteConnection>();
                    if (sqliteConnection is not null)
                    {
                        options.UseSqlite(sqliteConnection);
                    }
                    else
                    {
                        options.UseSqlite("Data Source=:memory:");
                    }
                    return;
                }

                // Production/dev: PostgreSQL
                // Check multiple sources for connection string:
                // 1. ConnectionStrings:DefaultConnection (standard ASP.NET Core)
                // 2. DATABASE_URL (fly.io convention)
                // 3. Database:ConnectionString (legacy/custom)
                var connectionString = configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = configuration["DATABASE_URL"];
                }
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    connectionString = configuration["Database:ConnectionString"];
                }
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        "Database connection string is not configured. "
                            + "Set ConnectionStrings:DefaultConnection, DATABASE_URL environment variable, or Database:ConnectionString."
                    );
                }

                // Convert DATABASE_URL format (postgres://user:pass@host:port/db) to Npgsql format if needed
                if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
                {
                    connectionString = ConvertDatabaseUrlToConnectionString(connectionString);
                }

                options.UseNpgsql(connectionString);
            }
        );

        services.AddScoped<IPingPongDbContext>(sp => sp.GetRequiredService<PingPongDbContext>());
        services.AddScoped<IMatchSubmissionService, MatchSubmissionService>();
        services.AddScoped<IStandingsService, StandingsService>();
        services.AddScoped<IPlayerDirectory, PlayerDirectory>();
        services.AddScoped<IHistoryService, HistoryService>();
        services.AddScoped<IHeadToHeadService, HeadToHeadService>();
        services.AddScoped<IRatingService, EloRatingService>();
        services.AddScoped<ITournamentCommandService, TournamentCommandService>();
        services.AddScoped<ITournamentQueryService, TournamentQueryService>();
        services.AddScoped<DevDataSeeder>();

        return services;
    }

    private static string ConvertDatabaseUrlToConnectionString(string databaseUrl)
    {
        // Parse postgres://user:password@host:port/database?params
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');

        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');
        var username = userInfo.Length > 0 ? userInfo[0] : "";
        var password = userInfo.Length > 1 ? userInfo[1] : "";

        // Parse query parameters for SSL mode
        var sslMode = "Require";
        if (!string.IsNullOrEmpty(uri.Query))
        {
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
            if (queryParams["sslmode"] != null)
            {
                sslMode = queryParams["sslmode"] switch
                {
                    "disable" => "Disable",
                    "require" => "Require",
                    "prefer" => "Prefer",
                    _ => "Require",
                };
            }
        }

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode={sslMode}";
    }
}
