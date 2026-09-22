using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WealthOps.Application.Common.Persistence;
using WealthOps.Domain.Entities;
using WealthOps.Domain.Enums;
using WealthOps.Domain.ValueObjects;
using WealthOps.Infrastructure.Persistence;
using WealthOps.Infrastructure.Persistence.Stores;
using WealthOps.TestFramework.Fixtures;

namespace WealthOps.Infrastructure.ComponentTest.Persistence;

/// <summary>
/// Idempotent recording against a real database — the guarantee behind BR-11 and AC-1.
/// </summary>
[Collection("Aspire")]
public sealed class DocumentStoreTests(AspireFixture aspire)
{
    [Fact]
    public async Task NewContentIsRecorded()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        await using WealthOpsDbContext dbContext = context.CreateContext();
        DocumentRecordResult result = await CreateStore(dbContext).RecordAsync(
            SyntheticDocument("sample.pdf", "synthetic content"),
            ct);

        result.Outcome.ShouldBe(DocumentRecordOutcome.Recorded);
        (await dbContext.Documents.CountAsync(ct)).ShouldBe(1);
    }

    [Fact]
    public async Task ReIngestingAnUnchangedFileChangesNothing()
    {
        // AC-1, stated directly. This is what lets the operator re-drop an export without
        // tracking what has already been loaded.
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        await using (WealthOpsDbContext first = context.CreateContext())
        {
            await CreateStore(first).RecordAsync(SyntheticDocument("sample.pdf", "synthetic content"), ct);
        }

        Guid originalId;
        await using (WealthOpsDbContext second = context.CreateContext())
        {
            DocumentRecordResult result = await CreateStore(second).RecordAsync(
                SyntheticDocument("sample.pdf", "synthetic content"),
                ct);

            result.Outcome.ShouldBe(DocumentRecordOutcome.AlreadyKnown);
            originalId = result.Document.Id;
        }

        await using WealthOpsDbContext read = context.CreateContext();
        Document stored = await read.Documents.SingleAsync(ct);
        stored.Id.ShouldBe(originalId);
    }

    [Fact]
    public async Task TheSameContentAtANewPathRelocatesRatherThanDuplicating()
    {
        // Content identity outranks location. Recording a second row would double-count the file;
        // leaving the old path would leave a row pointing at nothing.
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        Guid originalId;
        await using (WealthOpsDbContext first = context.CreateContext())
        {
            DocumentRecordResult initial = await CreateStore(first).RecordAsync(
                SyntheticDocument("sample.pdf", "synthetic content"),
                ct);
            originalId = initial.Document.Id;
        }

        Document moved = SyntheticDocument(Path.Combine("archive", "sample.pdf"), "synthetic content");

        await using (WealthOpsDbContext second = context.CreateContext())
        {
            DocumentRecordResult result = await CreateStore(second).RecordAsync(moved, ct);
            result.Outcome.ShouldBe(DocumentRecordOutcome.Relocated);
        }

        await using WealthOpsDbContext read = context.CreateContext();
        Document stored = await read.Documents.SingleAsync(ct);

        stored.Id.ShouldBe(originalId);
        stored.AbsolutePath.ShouldBe(moved.AbsolutePath);
    }

    [Fact]
    public async Task DifferentContentAtTheSamePathIsASeparateDocument()
    {
        // An export refreshed in place is genuinely new content, not the same document.
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        await using (WealthOpsDbContext first = context.CreateContext())
        {
            await CreateStore(first).RecordAsync(SyntheticDocument("sample.pdf", "synthetic content"), ct);
        }

        await using (WealthOpsDbContext second = context.CreateContext())
        {
            DocumentRecordResult result = await CreateStore(second).RecordAsync(
                SyntheticDocument("sample.pdf", "different synthetic content"),
                ct);

            result.Outcome.ShouldBe(DocumentRecordOutcome.Recorded);
        }

        await using WealthOpsDbContext read = context.CreateContext();
        (await read.Documents.CountAsync(ct)).ShouldBe(2);
    }

    [Fact]
    public async Task FindByContentHashLocatesAStoredDocument()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        Document document = SyntheticDocument("sample.pdf", "synthetic content");

        await using (WealthOpsDbContext write = context.CreateContext())
        {
            await CreateStore(write).RecordAsync(document, ct);
        }

        await using WealthOpsDbContext read = context.CreateContext();
        Document? found = await CreateStore(read).FindByContentHashAsync(document.ContentHash, ct);

        found.ShouldNotBeNull();
        found.ContentHash.ShouldBe(document.ContentHash);
    }

    [Fact]
    public async Task FindByContentHashReturnsNullForUnknownContent()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        await using WealthOpsDbContext read = context.CreateContext();

        (await CreateStore(read).FindByContentHashAsync(
            ContentHash.FromBytes("never stored"u8), ct)).ShouldBeNull();
    }

    [Fact]
    public async Task OptionalMetadataRoundTrips()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        var document = Document.Create(
            Path.Combine(Path.GetTempPath(), "wealthops-synthetic", "statement.pdf"),
            DocumentType.TaxStatement,
            ContentHash.FromBytes("synthetic statement"u8),
            Corpus.Personal,
            new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero),
            taxpayerId: "taxpayer-a",
            taxYear: TaxYear.Create(2025));

        await using (WealthOpsDbContext write = context.CreateContext())
        {
            await CreateStore(write).RecordAsync(document, ct);
        }

        await using WealthOpsDbContext read = context.CreateContext();
        Document stored = await read.Documents.SingleAsync(ct);

        // The nullable TaxYear conversion is the interesting part — a null here must survive as
        // null rather than becoming a default year.
        stored.TaxpayerId.ShouldBe("taxpayer-a");
        stored.TaxYear!.Value.Value.ShouldBe(2025);
        stored.DocumentType.ShouldBe(DocumentType.TaxStatement);
    }

    [Fact]
    public async Task ADocumentWithoutOptionalMetadataRoundTripsAsNull()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        await using (WealthOpsDbContext write = context.CreateContext())
        {
            await CreateStore(write).RecordAsync(SyntheticDocument("sample.pdf", "synthetic content"), ct);
        }

        await using WealthOpsDbContext read = context.CreateContext();
        Document stored = await read.Documents.SingleAsync(ct);

        stored.TaxpayerId.ShouldBeNull();
        stored.TaxYear.ShouldBeNull();
    }

    [Fact]
    public async Task CountReportsStoredDocuments()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        await using WealthOpsDbContext dbContext = context.CreateContext();
        IDocumentStore store = CreateStore(dbContext);

        (await store.CountAsync(ct)).ShouldBe(0);

        await store.RecordAsync(SyntheticDocument("a.pdf", "content a"), ct);
        await store.RecordAsync(SyntheticDocument("b.pdf", "content b"), ct);

        (await store.CountAsync(ct)).ShouldBe(2);
    }

    private static DocumentStore CreateStore(WealthOpsDbContext dbContext)
        => new(dbContext, NullLogger<DocumentStore>.Instance);

    private static Document SyntheticDocument(string relativePath, string content) => Document.Create(
        Path.Combine(Path.GetTempPath(), "wealthops-synthetic", relativePath),
        DocumentType.Payslip,
        ContentHash.FromBytes(System.Text.Encoding.UTF8.GetBytes(content)),
        Corpus.Personal,
        new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero));
}
