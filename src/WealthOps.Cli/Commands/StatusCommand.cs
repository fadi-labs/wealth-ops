using Mediator;
using WealthOps.Application.Common.Clients;
using WealthOps.Application.Common.Persistence;
using WealthOps.Application.Features.Diagnostics;

namespace WealthOps.Cli.Commands;

/// <summary>
/// Prints what is configured against what is actually there.
/// </summary>
/// <remarks>
/// The M0 acceptance probe. Its exit code is meaningful — non-zero when anything is unhealthy —
/// so it can gate a script as well as inform a person.
/// </remarks>
internal static class StatusCommand
{
    public static async Task<int> RunAsync(IMediator mediator, TextWriter output, CancellationToken cancellationToken)
    {
        GetSystemStatus.Response status =
            await mediator.Send(new GetSystemStatus.Request(), cancellationToken);

        WriteModelSection(status, output);
        output.WriteLine();
        WriteDatabaseSection(status, output);
        output.WriteLine();
        WriteEmbeddingSection(status, output);
        output.WriteLine();
        WriteDirectorySection(status, output);
        output.WriteLine();

        output.WriteLine(status.IsHealthy
            ? "OK — every checked component is usable."
            : "DEGRADED — see the entries marked [!] above.");

        return status.IsHealthy ? ExitCodes.Success : ExitCodes.Unhealthy;
    }

    private static void WriteModelSection(GetSystemStatus.Response status, TextWriter output)
    {
        ModelEndpointStatus endpoint = status.ModelEndpoint;

        output.WriteLine("Model gateway");
        output.WriteLine($"  {Mark(endpoint.IsReachable)} endpoint          {endpoint.Endpoint}");

        if (!endpoint.IsReachable)
        {
            output.WriteLine($"      {endpoint.FailureReason}");
            output.WriteLine("      Local inference runs as a host process, not as part of the stack —");
            output.WriteLine("      start it, or correct WealthOps:Models:Endpoint.");
        }

        output.WriteLine($"  {Mark(true)} chat model        {status.ChatModelId}");
        output.WriteLine($"  {Mark(true)} embedding model   {status.EmbeddingModelId} ({status.ConfiguredDimensions} dimensions)");

        if (endpoint is { IsReachable: true, AvailableModelIds.Count: > 0 })
        {
            // An advertised list that omits a configured model is the usual cause of a working
            // endpoint that still cannot answer.
            bool chatAdvertised = endpoint.AvailableModelIds.Contains(status.ChatModelId, StringComparer.Ordinal);
            bool embeddingAdvertised = endpoint.AvailableModelIds.Contains(status.EmbeddingModelId, StringComparer.Ordinal);

            if (!chatAdvertised || !embeddingAdvertised)
            {
                output.WriteLine($"  {Mark(false)} the endpoint does not advertise every configured model.");
                output.WriteLine($"      advertised: {string.Join(", ", endpoint.AvailableModelIds)}");
            }
        }
    }

    private static void WriteDatabaseSection(GetSystemStatus.Response status, TextWriter output)
    {
        PersistenceStatus persistence = status.Persistence;

        output.WriteLine("Database");
        output.WriteLine($"  {Mark(persistence.CanConnect)} connectivity");

        // Without a connection nothing downstream was actually measured. Reporting an unmeasured
        // value as [ok] would be worse than saying nothing: "schema current" beside a failed
        // connection reads as reassurance, and this command exists to be trusted when the stack
        // is broken.
        if (!persistence.CanConnect)
        {
            output.WriteLine($"  {Unknown} pgvector extension  not checked — no connection");
            output.WriteLine($"  {Unknown} schema              not checked — no connection");
            output.WriteLine($"  {Unknown} documents           not counted — no connection");

            if (persistence.FailureReason is not null)
            {
                output.WriteLine($"      {persistence.FailureReason}");
            }

            return;
        }

        output.WriteLine($"  {Mark(persistence.IsVectorExtensionInstalled)} pgvector extension");

        bool schemaCurrent = persistence.PendingMigrations.Count == 0;
        output.WriteLine($"  {Mark(schemaCurrent)} schema            " +
                         (schemaCurrent ? "current" : $"{persistence.PendingMigrations.Count} migration(s) pending"));

        if (!schemaCurrent)
        {
            foreach (string migration in persistence.PendingMigrations)
            {
                output.WriteLine($"      pending: {migration}");
            }

            output.WriteLine("      Run `wealthops ingest` or `wealthops chat` to apply them.");
        }

        if (persistence.FailureReason is not null)
        {
            output.WriteLine($"      {persistence.FailureReason}");
        }

        // Only meaningful once the schema is current — before that the query could not have run.
        output.WriteLine(schemaCurrent
            ? $"  {Mark(true)} documents         {status.DocumentCount}"
            : $"  {Unknown} documents           not counted — schema not current");
    }

    private static void WriteEmbeddingSection(GetSystemStatus.Response status, TextWriter output)
    {
        output.WriteLine("Stored embeddings");

        // The handler only queries these once the database is reachable and the schema is
        // current. Outside that, an empty list means "not asked", not "none stored".
        bool measured = status.Persistence is { CanConnect: true, PendingMigrations.Count: 0 };

        if (!measured)
        {
            output.WriteLine($"  {Unknown} not read — the database was not in a state to be queried.");
            return;
        }

        if (status.StoredEmbeddingProfiles.Count == 0)
        {
            output.WriteLine("    none stored yet — chunking and embedding arrive in M1.");
            return;
        }

        foreach (EmbeddingProfile profile in status.StoredEmbeddingProfiles)
        {
            bool matches = profile.Dimensions == status.ConfiguredDimensions
                           && string.Equals(profile.EmbeddingModelId, status.EmbeddingModelId, StringComparison.Ordinal);

            output.WriteLine($"  {Mark(matches)} {profile.EmbeddingModelId} " +
                             $"({profile.Dimensions} dimensions, {profile.ChunkCount} chunks)");
        }

        if (status.MismatchedEmbeddingProfiles.Count > 0)
        {
            output.WriteLine("      Stored vectors were produced by a different model or width than the one");
            output.WriteLine("      configured now. They are not comparable with anything the current model");
            output.WriteLine("      produces — re-embed the affected corpus, or point configuration back.");
        }
    }

    private static void WriteDirectorySection(GetSystemStatus.Response status, TextWriter output)
    {
        output.WriteLine("Directories");
        output.WriteLine($"  {Mark(status.PersonalDataDirectoryExists)} personal data     {status.PersonalDataDirectory}");
        output.WriteLine($"  {Mark(status.RulesCacheDirectoryExists)} rules cache       {status.RulesCacheDirectory}");

        if (!status.PersonalDataDirectoryExists)
        {
            output.WriteLine($"      Create it, or set {Configuration.PersonalDataDirectoryConfigurationSource.EnvironmentVariableName}.");
        }
    }

    private static string Mark(bool ok) => ok ? "[ok]" : "[!] ";

    /// <summary>Not measured, as opposed to measured-and-bad. Never rendered as <c>[ok]</c>.</summary>
    private const string Unknown = "[?] ";
}
