using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WealthOps.Application.Common.Clients;
using WealthOps.Application.Common.Configuration;
using WealthOps.Application.Common.Persistence;

namespace WealthOps.Application.Features.Diagnostics;

/// <summary>
/// Reconciles configuration against the model endpoint and the database.
/// </summary>
/// <remarks>
/// <para>
/// This is the M0 acceptance probe: it is the one place where "what is configured", "what the
/// endpoint actually offers" and "what the database actually holds" are compared. The interesting
/// case is the last of those — the embedding dimension recorded in stored data versus the one
/// configured now (ADR-002). A mismatch there is invisible everywhere else until retrieval quietly
/// starts returning nonsense.
/// </para>
/// <para>
/// Nothing here throws for a broken environment. Every collaborator reports failure as data,
/// because this command exists to be run on a machine where something is wrong.
/// </para>
/// </remarks>
public static class GetSystemStatus
{
    public sealed record Request : IRequest<Response>;

    /// <param name="ModelEndpoint">Endpoint reachability and advertised models.</param>
    /// <param name="ChatModelId">The configured chat model.</param>
    /// <param name="EmbeddingModelId">The configured embedding model.</param>
    /// <param name="ConfiguredDimensions">The dimension configuration declares for that model.</param>
    /// <param name="Persistence">Database connectivity, pgvector presence, pending migrations.</param>
    /// <param name="StoredEmbeddingProfiles">Model/dimension combinations actually present in the store.</param>
    /// <param name="DocumentCount">How many documents are recorded.</param>
    /// <param name="PersonalDataDirectory">The configured personal data root.</param>
    /// <param name="PersonalDataDirectoryExists">Whether that root exists on this machine.</param>
    /// <param name="RulesCacheDirectory">The configured rules cache root.</param>
    /// <param name="RulesCacheDirectoryExists">Whether that root exists on this machine.</param>
    public sealed record Response(
        ModelEndpointStatus ModelEndpoint,
        string ChatModelId,
        string EmbeddingModelId,
        int ConfiguredDimensions,
        PersistenceStatus Persistence,
        IReadOnlyList<EmbeddingProfile> StoredEmbeddingProfiles,
        int DocumentCount,
        string PersonalDataDirectory,
        bool PersonalDataDirectoryExists,
        string RulesCacheDirectory,
        bool RulesCacheDirectoryExists)
    {
        /// <summary>
        /// Stored dimensions that disagree with the configured one.
        /// </summary>
        /// <remarks>
        /// Non-empty means some stored vectors cannot be compared against anything the currently
        /// configured model produces. Either re-embed, or point configuration back at the model
        /// that wrote them.
        /// </remarks>
        public IReadOnlyList<EmbeddingProfile> MismatchedEmbeddingProfiles
            => [.. StoredEmbeddingProfiles.Where(p =>
                p.Dimensions != ConfiguredDimensions
                || !string.Equals(p.EmbeddingModelId, EmbeddingModelId, StringComparison.Ordinal))];

        /// <summary>Whether every checked component is in a usable state.</summary>
        public bool IsHealthy
            => ModelEndpoint.IsReachable
               && Persistence.CanConnect
               && Persistence.IsVectorExtensionInstalled
               && Persistence.PendingMigrations.Count == 0
               && MismatchedEmbeddingProfiles.Count == 0;
    }

    public sealed class Validator : AbstractValidator<Request>
    {
        // No input to constrain. Present because every request carries a validator by convention,
        // so adding one later is never a question of whether the pipeline is wired.
    }

    public sealed class Handler(
        IModelEndpointProbe endpointProbe,
        IChatModel chatModel,
        IEmbeddingModel embeddingModel,
        IPersistenceDiagnostics persistenceDiagnostics,
        IVectorStore vectorStore,
        IDocumentStore documentStore,
        IOptions<WealthOpsOptions> options,
        ILogger<Handler> logger)
        : IRequestHandler<Request, Response>
    {
        public async ValueTask<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            logger.LogInformation("Status probe started");

            WealthOpsOptions configuration = options.Value;

            ModelEndpointStatus endpoint = await endpointProbe.ProbeAsync(cancellationToken);
            PersistenceStatus persistence = await persistenceDiagnostics.ProbeAsync(cancellationToken);

            // Only query stored data once the database is known to be usable — probing a dead
            // connection would replace a clear diagnosis with a stack trace.
            IReadOnlyList<EmbeddingProfile> profiles = [];
            int documentCount = 0;

            if (persistence is { CanConnect: true, PendingMigrations.Count: 0 })
            {
                profiles = await vectorStore.GetEmbeddingProfilesAsync(cancellationToken);
                documentCount = await documentStore.CountAsync(cancellationToken);
            }

            var response = new Response(
                endpoint,
                chatModel.ModelId,
                embeddingModel.ModelId,
                embeddingModel.Dimensions,
                persistence,
                profiles,
                documentCount,
                configuration.PersonalDataDirectory,
                DirectoryExists(configuration.PersonalDataDirectory),
                configuration.RulesCacheDirectory,
                DirectoryExists(configuration.RulesCacheDirectory));

            logger.LogInformation("Status probe completed. Healthy: {IsHealthy}", response.IsHealthy);

            return response;
        }

        private static bool DirectoryExists(string path)
            => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
    }
}
