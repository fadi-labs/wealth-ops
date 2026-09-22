using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using WealthOps.Application.Common.Clients;
using WealthOps.Domain.ValueObjects;
using WealthOps.Infrastructure.Persistence;
using WealthOps.TestFramework.Fixtures;

namespace WealthOps.Infrastructure.ComponentTest.Persistence;

/// <summary>
/// An isolated, migrated database plus a live <see cref="WealthOpsDbContext"/> over it.
/// </summary>
/// <remarks>
/// Uses a real PostgreSQL instance rather than the in-memory provider, because the things under
/// test here — the <c>vector</c> column, cosine distance ordering, and the unique index that makes
/// re-ingestion idempotent — have no in-memory equivalent. A green in-memory test would prove
/// nothing about any of them.
/// </remarks>
internal sealed class PersistenceTestContext : IAsyncDisposable
{
    private readonly WealthOpsTestDatabase _database;
    private readonly NpgsqlDataSource _dataSource;

    private PersistenceTestContext(WealthOpsTestDatabase database, NpgsqlDataSource dataSource)
    {
        _database = database;
        _dataSource = dataSource;
    }

    public static async Task<PersistenceTestContext> CreateAsync(
        AspireFixture aspire,
        CancellationToken cancellationToken)
    {
        WealthOpsTestDatabase database = await WealthOpsTestDatabase.CreateAsync(
            aspire,
            $"infra-component-{Guid.NewGuid():N}",
            cancellationToken,
            initialize: async (connectionString, ct) =>
            {
                // The schema comes from the real migration, so this also proves the migration
                // applies — including CREATE EXTENSION vector.
                await using NpgsqlDataSource migrationDataSource = BuildDataSource(connectionString);
                await using var context = CreateDbContext(migrationDataSource);
                await context.Database.MigrateAsync(ct);
            });

        return new PersistenceTestContext(database, BuildDataSource(database.ConnectionString));
    }

    /// <summary>A fresh context per call — mirrors the scoped registration in production.</summary>
    public WealthOpsDbContext CreateContext() => CreateDbContext(_dataSource);

    /// <summary>An embedding model stub of the given width. No language model is involved (NFR-3).</summary>
    public static IEmbeddingModel EmbeddingModel(int dimensions = 4)
        => new StubEmbeddingModel(dimensions);

    public async ValueTask DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await _database.DisposeAsync();
    }

    private static NpgsqlDataSource BuildDataSource(string connectionString)
    {
        // SSL is disabled against the plain test container, matching the other fixtures.
        var builder = new NpgsqlDataSourceBuilder($"{connectionString};SSL Mode=Disable");
        builder.UseVector();
        return builder.Build();
    }

    private static WealthOpsDbContext CreateDbContext(NpgsqlDataSource dataSource)
        => new(new DbContextOptionsBuilder<WealthOpsDbContext>()
            .UseNpgsql(dataSource, npgsql => npgsql.UseVector())
            .UseLoggerFactory(NullLoggerFactory.Instance)
            .Options);

    private sealed class StubEmbeddingModel(int dimensions) : IEmbeddingModel
    {
        public string ModelId => "synthetic-embedding-model";

        public int Dimensions => dimensions;

        public Task<EmbeddingVector> EmbedAsync(string text, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Component tests supply vectors directly.");

        public Task<IReadOnlyList<EmbeddingVector>> EmbedAsync(
            IReadOnlyList<string> texts,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Component tests supply vectors directly.");
    }
}
