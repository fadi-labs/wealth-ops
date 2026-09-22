using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WealthOps.Infrastructure.Persistence.Extensions;

public static class MigrationExtensions
{
    /// <summary>
    /// Applies any pending migrations.
    /// </summary>
    /// <remarks>
    /// Migrating on startup is appropriate here in a way it would not be for a multi-instance
    /// service: this is a single-operator system (OOS-16) where the process that runs the
    /// migration is the only thing using the database. The alternative — a separate migration
    /// step — would be one more thing to remember on a tool whose whole purpose is to reduce
    /// manual upkeep.
    /// </remarks>
    public static async Task MigrateWealthOpsAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using AsyncServiceScope scope = services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<WealthOpsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(MigrationExtensions));

        string[] pending = [.. await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)];

        if (pending.Length == 0)
        {
            logger.LogDebug("Database schema is current; no migrations to apply");
            return;
        }

        logger.LogInformation("Database migration started. Pending migrations: {PendingCount}", pending.Length);

        await dbContext.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Database migration completed. Applied: {AppliedCount}", pending.Length);
    }
}
