using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace WealthOps.Infrastructure.Persistence.DesignTime;

/// <summary>
/// Builds a context for <c>dotnet ef</c> only.
/// </summary>
/// <remarks>
/// The connection string here is a design-time placeholder and is never used to reach a real
/// database — <c>dotnet ef migrations add</c> needs a provider to generate SQL, not a live server.
/// It deliberately carries no credential worth having: a real one in this file would be a
/// committed secret for no benefit. Set <c>WEALTHOPS_DESIGNTIME_CONNECTION</c> to override.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed class WealthOpsDbContextFactory : IDesignTimeDbContextFactory<WealthOpsDbContext>
{
    private const string PlaceholderConnection =
        "Host=localhost;Port=5432;Database=wealthops;Username=postgres;Password=design-time-only";

    public WealthOpsDbContext CreateDbContext(string[] args)
    {
        string connectionString =
            Environment.GetEnvironmentVariable("WEALTHOPS_DESIGNTIME_CONNECTION")
            ?? PlaceholderConnection;

        DbContextOptions<WealthOpsDbContext> options =
            new DbContextOptionsBuilder<WealthOpsDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
                .Options;

        return new WealthOpsDbContext(options);
    }
}
