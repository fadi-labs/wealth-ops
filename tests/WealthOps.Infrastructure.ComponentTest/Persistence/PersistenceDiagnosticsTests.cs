using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WealthOps.Application.Common.Persistence;
using WealthOps.Infrastructure.Persistence;
using WealthOps.Infrastructure.Persistence.Diagnostics;
using WealthOps.TestFramework.Fixtures;

namespace WealthOps.Infrastructure.ComponentTest.Persistence;

[Collection("Aspire")]
public sealed class PersistenceDiagnosticsTests(AspireFixture aspire)
{
    [Fact]
    public async Task AMigratedDatabaseReportsHealthy()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        await using WealthOpsDbContext dbContext = context.CreateContext();
        PersistenceStatus status = await CreateDiagnostics(dbContext).ProbeAsync(ct);

        status.CanConnect.ShouldBeTrue();
        // Proves the migration's CREATE EXTENSION actually ran, not merely that it was declared.
        status.IsVectorExtensionInstalled.ShouldBeTrue();
        status.PendingMigrations.ShouldBeEmpty();
        status.FailureReason.ShouldBeNull();
    }

    [Fact]
    public async Task AnUnreachableDatabaseIsReportedNotThrown()
    {
        // status must stay usable on a machine where the database is down — that is the case it
        // exists for.
        CancellationToken ct = TestContext.Current.CancellationToken;

        await using WealthOpsDbContext dbContext = UnreachableDbContext();
        PersistenceStatus status = await CreateDiagnostics(dbContext).ProbeAsync(ct);

        status.CanConnect.ShouldBeFalse();
        status.IsVectorExtensionInstalled.ShouldBeFalse();
        status.FailureReason.ShouldNotBeNullOrWhiteSpace();
    }

    private static PersistenceDiagnostics CreateDiagnostics(WealthOpsDbContext dbContext)
        => new(dbContext, NullLogger<PersistenceDiagnostics>.Instance);

    private static WealthOpsDbContext UnreachableDbContext()
    {
        // Port 1 with a short timeout: a deterministic refusal, not a hang.
        DbContextOptions<WealthOpsDbContext> options = new DbContextOptionsBuilder<WealthOpsDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=absent;Username=none;Password=none;Timeout=2;SSL Mode=Disable")
            .Options;

        return new WealthOpsDbContext(options);
    }
}
