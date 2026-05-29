using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;
using System.Threading;

namespace Tests.Integration.Fixtures;

/// <summary>
/// PostgreSQL test container + ApplicationDbContext fixture for integration tests.
/// </summary>
public class DatabaseFixture : IAsyncLifetime
{
    private static readonly SemaphoreSlim ContainerLock = new(1, 1);
    private static PostgreSqlContainer? _sharedContainer;
    private static bool _schemaMigrated;

    public ApplicationDbContext DbContext { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await ContainerLock.WaitAsync();
        try
        {
            if (_sharedContainer is null)
            {
                _sharedContainer = new PostgreSqlBuilder()
                    .WithImage("postgres:15")
                    .Build();

                await _sharedContainer.StartAsync();
            }

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(_sharedContainer.GetConnectionString())
                .Options;

            DbContext = new ApplicationDbContext(options);

            if (!_schemaMigrated)
            {
                await DbContext.Database.MigrateAsync();
                _schemaMigrated = true;
            }
        }
        finally
        {
            ContainerLock.Release();
        }
    }

    public async Task DisposeAsync()
    {
        if (DbContext is not null)
        {
            await DbContext.DisposeAsync();
        }
    }

    public async Task ResetAsync()
    {
        await DbContext.Database.EnsureDeletedAsync();
        await DbContext.Database.MigrateAsync();
    }
}
