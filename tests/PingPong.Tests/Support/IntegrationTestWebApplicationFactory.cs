using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PingPong.Infrastructure.Persistence;

namespace PingPong.Tests.Support;

public sealed class IntegrationTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<PingPongDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            _connection = connection;

            services.AddSingleton(connection);

            services.AddDbContext<PingPongDbContext>((sp, options) =>
            {
                var sqliteConnection = sp.GetRequiredService<SqliteConnection>();
                options.UseSqlite(sqliteConnection);
            });

            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var scopedServices = scope.ServiceProvider;
            var db = scopedServices.GetRequiredService<PingPongDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }
}
