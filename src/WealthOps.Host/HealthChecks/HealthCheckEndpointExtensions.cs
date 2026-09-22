using WealthOps.Application.Common.Persistence;

namespace WealthOps.Host.Configuration;

internal static class HealthCheckEndpointExtensions
{
    /// <summary>
    /// Maps <c>GET /health</c>.
    /// </summary>
    /// <remarks>
    /// Reports database reachability only. The model endpoint is deliberately excluded: it is an
    /// external host process whose absence is a normal state for this Host (see the AppHost), so
    /// folding it in would make the probe report unhealthy for something the Host neither owns nor
    /// uses. The full picture is what <c>wealthops status</c> is for.
    /// </remarks>
    public static WebApplication MapWealthOpsHealthChecks(this WebApplication app)
    {
        app.MapGet("/health", async (
            IPersistenceDiagnostics diagnostics,
            CancellationToken cancellationToken) =>
        {
            PersistenceStatus status = await diagnostics.ProbeAsync(cancellationToken);

            bool healthy = status is { CanConnect: true, IsVectorExtensionInstalled: true }
                           && status.PendingMigrations.Count == 0;

            var payload = new
            {
                status = healthy ? "Healthy" : "Unhealthy",
                database = new
                {
                    canConnect = status.CanConnect,
                    vectorExtension = status.IsVectorExtensionInstalled,
                    pendingMigrations = status.PendingMigrations.Count
                }
            };

            return healthy ? Results.Ok(payload) : Results.Json(payload, statusCode: 503);
        });

        return app;
    }
}
