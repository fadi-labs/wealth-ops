using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WealthOps.Application.Common.Configuration;
using WealthOps.Application.Common.Persistence;
using WealthOps.Domain.Entities;
using WealthOps.Domain.Enums;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Application.Features.Ingestion;

/// <summary>
/// Walks a path, hashes every file, and records each one idempotently.
/// </summary>
/// <remarks>
/// <para>
/// <strong>M0 scope.</strong> This records <em>that</em> a file was ingested and what it is — it
/// does not extract text, chunk, or embed. Those arrive in M1 (FR-M1-1, FR-M1-2). What is complete
/// here is the idempotence guarantee: re-dropping an unchanged export produces no change in stored
/// data (BR-11, AC-1), which is the property the operator relies on to stop tracking what has
/// already been loaded.
/// </para>
/// <para>
/// A path outside both configured roots is refused. Ingestion is not a general-purpose file
/// reader: the roots are what make "rules content is public, personal content is not" enforceable,
/// and a path that belongs to neither has no corpus to be assigned to.
/// </para>
/// </remarks>
public static class IngestPath
{
    /// <param name="Path">
    /// A file or directory. Absolute, or relative to the current working directory. Must resolve
    /// to somewhere under the configured personal data root or the configured rules cache root.
    /// </param>
    public sealed record Request(string Path) : IRequest<Response>;

    /// <param name="Corpus">Which corpus the path resolved to.</param>
    /// <param name="Recorded">Files whose content had not been seen before.</param>
    /// <param name="AlreadyKnown">Files whose content was already stored at the same path (AC-1).</param>
    /// <param name="Relocated">Files whose content was known under a different path.</param>
    /// <param name="Unclassified">
    /// Files recorded but not routed to a known document type. Reported so the operator can act,
    /// never silently dropped.
    /// </param>
    public sealed record Response(
        Corpus Corpus,
        int Recorded,
        int AlreadyKnown,
        int Relocated,
        int Unclassified)
    {
        public int TotalSeen => Recorded + AlreadyKnown + Relocated;
    }

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Path)
                .NotEmpty()
                .WithMessage("Supply a file or directory to ingest.");
        }
    }

    public sealed class Handler(
        IDocumentStore documentStore,
        IOptions<WealthOpsOptions> options,
        TimeProvider timeProvider,
        ILogger<Handler> logger)
        : IRequestHandler<Request, Response>
    {
        public async ValueTask<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            WealthOpsOptions configuration = options.Value;
            string fullPath = Path.GetFullPath(request.Path);

            (Corpus corpus, string root) = ResolveCorpus(fullPath, configuration);

            string[] files = EnumerateFiles(fullPath);

            // Path counts, never path contents or file names (NFR-8).
            logger.LogInformation(
                "Ingestion started. Corpus: {Corpus}. Files found: {FileCount}",
                corpus,
                files.Length);

            int recorded = 0;
            int alreadyKnown = 0;
            int relocated = 0;
            int unclassified = 0;

            DateTimeOffset ingestedAt = timeProvider.GetUtcNow();

            foreach (string file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                ContentHash hash = await ComputeHashAsync(file, cancellationToken);

                DocumentType documentType = corpus == Corpus.Rules
                    ? DocumentType.TaxRulePage
                    : PersonalDataRouter.Route(Path.GetRelativePath(root, file));

                if (documentType == DocumentType.Unclassified)
                {
                    unclassified++;
                }

                var document = Document.Create(
                    file,
                    documentType,
                    hash,
                    corpus,
                    ingestedAt);

                DocumentRecordResult result = await documentStore.RecordAsync(document, cancellationToken);

                switch (result.Outcome)
                {
                    case DocumentRecordOutcome.Recorded:
                        recorded++;
                        break;
                    case DocumentRecordOutcome.AlreadyKnown:
                        alreadyKnown++;
                        break;
                    case DocumentRecordOutcome.Relocated:
                        relocated++;
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unhandled document record outcome '{result.Outcome}'.");
                }
            }

            logger.LogInformation(
                "Ingestion completed. Corpus: {Corpus}. Recorded: {Recorded}. Already known: {AlreadyKnown}. " +
                "Relocated: {Relocated}. Unclassified: {Unclassified}",
                corpus,
                recorded,
                alreadyKnown,
                relocated,
                unclassified);

            return new Response(corpus, recorded, alreadyKnown, relocated, unclassified);
        }

        private static (Corpus Corpus, string Root) ResolveCorpus(string fullPath, WealthOpsOptions configuration)
        {
            if (IsUnder(fullPath, configuration.RulesCacheDirectory))
            {
                return (Corpus.Rules, Path.GetFullPath(configuration.RulesCacheDirectory));
            }

            if (IsUnder(fullPath, configuration.PersonalDataDirectory))
            {
                return (Corpus.Personal, Path.GetFullPath(configuration.PersonalDataDirectory));
            }

            throw new DirectoryNotFoundException(
                "The path is not under a configured corpus root. Ingest from either " +
                "WealthOps:PersonalDataDirectory or WealthOps:RulesCacheDirectory — every ingested " +
                "file must belong to exactly one corpus.");
        }

        private static bool IsUnder(string candidate, string root)
        {
            if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
            {
                return false;
            }

            string normalisedRoot =
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
            string normalisedCandidate =
                Path.TrimEndingDirectorySeparator(candidate) + Path.DirectorySeparatorChar;

            return normalisedCandidate.StartsWith(normalisedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string[] EnumerateFiles(string fullPath)
        {
            if (File.Exists(fullPath))
            {
                return [fullPath];
            }

            if (Directory.Exists(fullPath))
            {
                return [.. Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal)];
            }

            throw new FileNotFoundException("No file or directory exists at the supplied path.", fullPath);
        }

        private static async Task<ContentHash> ComputeHashAsync(string file, CancellationToken cancellationToken)
        {
            await using FileStream stream = File.Open(
                file,
                new FileStreamOptions
                {
                    Mode = FileMode.Open,
                    Access = FileAccess.Read,
                    Share = FileShare.Read,
                    Options = FileOptions.Asynchronous | FileOptions.SequentialScan
                });

            return await ContentHash.FromStreamAsync(stream, cancellationToken);
        }
    }
}
