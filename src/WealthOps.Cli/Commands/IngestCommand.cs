using Mediator;
using WealthOps.Application.Features.Ingestion;

namespace WealthOps.Cli.Commands;

/// <summary>
/// Records the files under a path, idempotently.
/// </summary>
/// <remarks>
/// M0 records what a file is and that it was seen; text extraction, chunking and embedding arrive
/// in M1. The output says so explicitly rather than implying the corpus is queryable.
/// </remarks>
internal static class IngestCommand
{
    public static async Task<int> RunAsync(
        IMediator mediator,
        string path,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        IngestPath.Response result =
            await mediator.Send(new IngestPath.Request(path), cancellationToken);

        output.WriteLine($"Corpus:        {result.Corpus}");
        output.WriteLine($"Files seen:    {result.TotalSeen}");
        output.WriteLine($"  recorded:    {result.Recorded}");
        output.WriteLine($"  unchanged:   {result.AlreadyKnown}");
        output.WriteLine($"  relocated:   {result.Relocated}");

        if (result.Unclassified > 0)
        {
            output.WriteLine();
            output.WriteLine($"[!] {result.Unclassified} file(s) did not match a known folder and were recorded");
            output.WriteLine("    as Unclassified. Move them into the documented layout, or they will be");
            output.WriteLine("    skipped by the parsers that arrive in M2.");
        }

        output.WriteLine();
        output.WriteLine("Recorded only — text extraction and embedding arrive in M1.");

        return ExitCodes.Success;
    }
}
