using Microsoft.EntityFrameworkCore;
using WealthOps.Domain.Entities;

namespace WealthOps.Infrastructure.Persistence;

/// <summary>
/// The application database.
/// </summary>
/// <remarks>
/// Registered scoped. Mediator handlers are registered scoped for the same reason — a singleton
/// handler holding this context would fail DI scope validation at startup.
/// </remarks>
public sealed class WealthOpsDbContext(DbContextOptions<WealthOpsDbContext> options) : DbContext(options)
{
    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Declared on the model so the initial migration emits CREATE EXTENSION, rather than the
        // extension being a manual setup step someone has to remember.
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WealthOpsDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
