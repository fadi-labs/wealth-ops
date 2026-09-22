namespace WealthOps.Application.Common.Clients;

/// <summary>
/// What <c>status</c> could learn about the configured model endpoint.
/// </summary>
/// <param name="Endpoint">The configured base address, echoed back for display.</param>
/// <param name="IsReachable">Whether the endpoint answered at all.</param>
/// <param name="AvailableModelIds">
/// Model identifiers the endpoint advertises. Empty when unreachable, or when the endpoint does
/// not implement a model listing — absence is not evidence that a model is missing.
/// </param>
/// <param name="FailureReason">Why the probe failed, for display. <see langword="null"/> on success.</param>
public sealed record ModelEndpointStatus(
    Uri Endpoint,
    bool IsReachable,
    IReadOnlyList<string> AvailableModelIds,
    string? FailureReason);

/// <summary>
/// Reports whether the configured model endpoint is answering.
/// </summary>
/// <remarks>
/// Separate from <see cref="IChatModel"/> because probing must not cost a completion, and because
/// the probe has to report failure as data rather than throw — <c>status</c> exists precisely to
/// be run on a machine where something is wrong.
/// </remarks>
public interface IModelEndpointProbe
{
    /// <summary>Probes the endpoint. Never throws for an unreachable endpoint; reports it instead.</summary>
    Task<ModelEndpointStatus> ProbeAsync(CancellationToken cancellationToken = default);
}
