using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using WealthOps.Application.Common.Persistence;

namespace WealthOps.Infrastructure.Persistence.Diagnostics;

/// <summary>
/// Probes the database for <c>status</c>.
/// </summary>
/// <remarks>
/// Reports failure as data rather than throwing — see <see cref="IPersistenceDiagnostics"/>.
/// The pgvector check is separate from connectivity because "connects but has no vector extension"
/// is by far the most likely way this stack is misconfigured, and it looks like a healthy database
/// right up until the first migration or search.
/// </remarks>
internal sealed class PersistenceDiagnostics(
    WealthOpsDbContext dbContext,
    ILogger<PersistenceDiagnostics> logger)
    : IPersistenceDiagnostics
{
    public async Task<PersistenceStatus> ProbeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            bool canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                return new PersistenceStatus(
                    CanConnect: false,
                    IsVectorExtensionInstalled: false,
                    PendingMigrations: [],
                    FailureReason: "The database did not accept a connection.");
            }

            bool hasVector = await HasVectorExtensionAsync(cancellationToken);

            IReadOnlyList<string> pending =
                [.. await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)];

            logger.LogDebug(
                "Persistence probe succeeded. pgvector: {HasVector}. Pending migrations: {PendingCount}",
                hasVector,
                pending.Count);

            return new PersistenceStatus(
                CanConnect: true,
                IsVectorExtensionInstalled: hasVector,
                PendingMigrations: pending,
                FailureReason: hasVector
                    ? null
                    : "The database is reachable but the 'vector' extension is not installed. " +
                      "Confirm the PostgreSQL image is pgvector-enabled and that migrations have been applied.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug("Persistence probe failed: {Reason}", ex.Message);

            return new PersistenceStatus(
                CanConnect: false,
                IsVectorExtensionInstalled: false,
                PendingMigrations: [],
                FailureReason: ex.Message);
        }
    }

    private async Task<bool> HasVectorExtensionAsync(CancellationToken cancellationToken)
    {
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'vector');";

        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            object? result = await command.ExecuteScalarAsync(cancellationToken);
            return result is true;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
