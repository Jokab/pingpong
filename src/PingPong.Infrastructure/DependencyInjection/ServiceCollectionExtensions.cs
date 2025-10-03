using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PingPong.Application.Interfaces;
using PingPong.Infrastructure.Persistence;
using PingPong.Infrastructure.Services;

namespace PingPong.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<PingPongDbContext>((serviceProvider, options) =>
        {
            var provider = configuration["Database:Provider"] ?? "Sqlite";

            if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                var connectionString = configuration.GetConnectionString("SqlServer")
                    ?? configuration["Database:ConnectionString"]
                    ?? throw new InvalidOperationException("SQL Server connection string is not configured.");

                options.UseSqlServer(connectionString);
            }
            else
            {
                var connectionString = configuration.GetConnectionString("Sqlite")
                    ?? configuration["Database:ConnectionString"]
                    ?? "Data Source=pingpong_dev.db";

                options.UseSqlite(connectionString);
            }
        });

        services.AddScoped<IMatchSubmissionService, MatchSubmissionService>();
        services.AddScoped<IStandingsService, StandingsService>();
        services.AddScoped<IPlayerDirectory, PlayerDirectory>();
        services.AddScoped<IHistoryService, HistoryService>();

        return services;
    }
}
