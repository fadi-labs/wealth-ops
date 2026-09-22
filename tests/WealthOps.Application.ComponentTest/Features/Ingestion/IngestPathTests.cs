using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WealthOps.Application.Common.Configuration;
using WealthOps.Application.Common.Persistence;
using WealthOps.Application.Features.Ingestion;
using WealthOps.Domain.Entities;
using WealthOps.Domain.Enums;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Application.ComponentTest.Features.Ingestion;

/// <summary>
/// Walk, hash, route and record — against a real temporary directory tree.
/// </summary>
/// <remarks>
/// <para>
/// L1 rather than L0 because the behaviour under test <em>is</em> the file walk: routing by folder
/// position, hashing file content, and doing nothing the second time. A mocked file system would
/// only re-assert the mock.
/// </para>
/// <para>
/// Every fixture is invented and lives under the system temp directory. Nothing here reads a real
/// document (BR-12, BR-15).
/// </para>
/// </remarks>
public sealed class IngestPathTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"wealthops-synthetic-{Guid.NewGuid():N}");

    private readonly string _rulesCache = Path.Combine(
        Path.GetTempPath(),
        $"wealthops-synthetic-rules-{Guid.NewGuid():N}");

    [Fact]
    public async Task FilesAreRoutedByTheirPositionInTheTree()
    {
        WriteFile("transactions/format-a/export.csv", "synthetic format a content");
        WriteFile("transactions/format-b/export.csv", "synthetic format b content");
        WriteFile("documents/payslips/2026-01.pdf", "synthetic payslip content");
        WriteFile("reference/instruments.csv", "synthetic instrument content");

        var store = new RecordingDocumentStore();
        IngestPath.Response response = await Handler(store).Handle(
            new IngestPath.Request(_root),
            TestContext.Current.CancellationToken);

        response.Corpus.ShouldBe(Corpus.Personal);
        response.Recorded.ShouldBe(4);
        response.Unclassified.ShouldBe(0);

        store.Documents.Select(d => d.DocumentType).ShouldBe(
            [
                DocumentType.Payslip,
                DocumentType.Reference,
                DocumentType.TransactionExportFormatA,
                DocumentType.TransactionExportFormatB
            ],
            ignoreOrder: true);
    }

    [Fact]
    public async Task ReIngestingAnUnchangedTreeRecordsNothingNew()
    {
        // AC-1 at the slice level: the operator can re-drop the whole tree safely.
        WriteFile("documents/payslips/2026-01.pdf", "synthetic payslip content");

        var store = new RecordingDocumentStore();
        IngestPath.Handler handler = Handler(store);

        await handler.Handle(new IngestPath.Request(_root), TestContext.Current.CancellationToken);
        IngestPath.Response second = await handler.Handle(
            new IngestPath.Request(_root),
            TestContext.Current.CancellationToken);

        second.Recorded.ShouldBe(0);
        second.AlreadyKnown.ShouldBe(1);
        second.TotalSeen.ShouldBe(1);
        store.Documents.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AChangedFileIsRecordedAsNewContent()
    {
        WriteFile("documents/payslips/2026-01.pdf", "synthetic payslip content");

        var store = new RecordingDocumentStore();
        IngestPath.Handler handler = Handler(store);

        await handler.Handle(new IngestPath.Request(_root), TestContext.Current.CancellationToken);

        WriteFile("documents/payslips/2026-01.pdf", "revised synthetic payslip content");

        IngestPath.Response second = await handler.Handle(
            new IngestPath.Request(_root),
            TestContext.Current.CancellationToken);

        second.Recorded.ShouldBe(1);
    }

    [Fact]
    public async Task UnroutableFilesAreRecordedAndReported()
    {
        // Reported rather than dropped (BR-10): a file in the wrong place is a fact the operator
        // needs, because the M2 parsers will not see it.
        WriteFile("documents/payslips/2026-01.pdf", "synthetic payslip content");
        WriteFile("stray.txt", "synthetic stray content");

        var store = new RecordingDocumentStore();
        IngestPath.Response response = await Handler(store).Handle(
            new IngestPath.Request(_root),
            TestContext.Current.CancellationToken);

        response.Recorded.ShouldBe(2);
        response.Unclassified.ShouldBe(1);
        store.Documents.ShouldContain(d => d.DocumentType == DocumentType.Unclassified);
    }

    [Fact]
    public async Task ASingleFileCanBeIngested()
    {
        string file = WriteFile("documents/payslips/2026-01.pdf", "synthetic payslip content");

        var store = new RecordingDocumentStore();
        IngestPath.Response response = await Handler(store).Handle(
            new IngestPath.Request(file),
            TestContext.Current.CancellationToken);

        response.TotalSeen.ShouldBe(1);
        response.Corpus.ShouldBe(Corpus.Personal);
    }

    [Fact]
    public async Task ThePathDecidesTheCorpus()
    {
        // The two roots are what make "rules content is public, personal content is not"
        // enforceable rather than procedural.
        Directory.CreateDirectory(_rulesCache);
        File.WriteAllText(Path.Combine(_rulesCache, "a-rule-page.txt"), "synthetic rules content");

        var store = new RecordingDocumentStore();
        IngestPath.Response response = await Handler(store).Handle(
            new IngestPath.Request(_rulesCache),
            TestContext.Current.CancellationToken);

        response.Corpus.ShouldBe(Corpus.Rules);
        store.Documents.ShouldHaveSingleItem().DocumentType.ShouldBe(DocumentType.TaxRulePage);
    }

    [Fact]
    public async Task APathOutsideBothRootsIsRefused()
    {
        // Ingestion is not a general-purpose file reader. A path belonging to neither root has no
        // corpus to be assigned to, so there is no safe default.
        string outside = Path.Combine(Path.GetTempPath(), $"wealthops-outside-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outside);

        try
        {
            await Should.ThrowAsync<DirectoryNotFoundException>(async () =>
                await Handler(new RecordingDocumentStore()).Handle(
                    new IngestPath.Request(outside),
                    TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(outside, recursive: true);
        }
    }

    [Fact]
    public async Task AMissingPathFailsLoudly()
    {
        await Should.ThrowAsync<FileNotFoundException>(async () =>
            await Handler(new RecordingDocumentStore()).Handle(
                new IngestPath.Request(Path.Combine(_root, "does-not-exist")),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IdenticalContentAtTwoPathsIsOneDocument()
    {
        WriteFile("documents/payslips/original.pdf", "synthetic payslip content");
        WriteFile("documents/payslips/copy.pdf", "synthetic payslip content");

        var store = new RecordingDocumentStore();
        IngestPath.Response response = await Handler(store).Handle(
            new IngestPath.Request(_root),
            TestContext.Current.CancellationToken);

        // Content identity, not path identity. The second file relocates the first rather than
        // duplicating it.
        response.TotalSeen.ShouldBe(2);
        response.Recorded.ShouldBe(1);
        response.Relocated.ShouldBe(1);
        store.Documents.Count.ShouldBe(1);
    }

    [Fact]
    public void AnEmptyPathIsRejectedByTheValidator()
        => new IngestPath.Validator().Validate(new IngestPath.Request("")).IsValid.ShouldBeFalse();

    public void Dispose()
    {
        foreach (string directory in new[] { _root, _rulesCache })
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private IngestPath.Handler Handler(IDocumentStore store) => new(
        store,
        Options.Create(new WealthOpsOptions
        {
            PersonalDataDirectory = _root,
            RulesCacheDirectory = _rulesCache
        }),
        TimeProvider.System,
        NullLogger<IngestPath.Handler>.Instance);

    private string WriteFile(string relativePath, string content)
    {
        string full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    /// <summary>In-memory stand-in with the same content-hash identity rule as the real store.</summary>
    private sealed class RecordingDocumentStore : IDocumentStore
    {
        private readonly Dictionary<string, Document> _byHash = [];

        public IReadOnlyList<Document> Documents => [.. _byHash.Values];

        public Task<DocumentRecordResult> RecordAsync(
            Document document,
            CancellationToken cancellationToken = default)
        {
            if (!_byHash.TryGetValue(document.ContentHash.Value, out Document? existing))
            {
                _byHash[document.ContentHash.Value] = document;
                return Task.FromResult(new DocumentRecordResult(document, DocumentRecordOutcome.Recorded));
            }

            if (string.Equals(existing.AbsolutePath, document.AbsolutePath, StringComparison.Ordinal))
            {
                return Task.FromResult(new DocumentRecordResult(existing, DocumentRecordOutcome.AlreadyKnown));
            }

            existing.RelocateTo(document.AbsolutePath);
            return Task.FromResult(new DocumentRecordResult(existing, DocumentRecordOutcome.Relocated));
        }

        public Task<Document?> FindByContentHashAsync(
            ContentHash contentHash,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_byHash.GetValueOrDefault(contentHash.Value));

        public Task<int> CountAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_byHash.Count);
    }
}
