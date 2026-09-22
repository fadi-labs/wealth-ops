namespace WealthOps.Application.Common.Persistence;

/// <summary>What <c>status</c> could learn about the database.</summary>
/// <param name="CanConnect">Whether a connection could be opened.</param>
/// <param name="IsVectorExtensionInstalled">
/// Whether <c>pgvector</c> is present. A database that connects but lacks the extension is the
/// single most likely misconfiguration in this stack, so it is reported separately rather than
/// folded into <paramref name="CanConnect"/>.
/// </param>
/// <param name="PendingMigrations">Migrations not yet applied. Empty when the schema is current.</param>
/// <param name="FailureReason">Why the probe failed. <see langword="null"/> on success.</param>
public sealed record PersistenceStatus(
    bool CanConnect,
    bool IsVectorExtensionInstalled,
    IReadOnlyList<string> PendingMigrations,
    string? FailureReason);

/// <summary>
/// Reports database reachability and schema state without throwing.
/// </summary>
/// <remarks>
/// Every method reports failure as data. <c>status</c> is the tool an operator reaches for when
/// the stack is broken, so it must never be the thing that breaks.
/// </remarks>
public interface IPersistenceDiagnostics
{
    Task<PersistenceStatus> ProbeAsync(CancellationToken cancellationToken = default);
}
