using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using WealthOps.Application.Common.Exceptions;
using WealthOps.Application.Common.Persistence;
using WealthOps.Domain.Entities;
using WealthOps.Domain.Enums;
using WealthOps.Domain.ValueObjects;
using WealthOps.Infrastructure.Persistence;
using WealthOps.Infrastructure.Persistence.Stores;
using WealthOps.TestFramework.Fixtures;

namespace WealthOps.Infrastructure.ComponentTest.Persistence;

/// <summary>
/// Vector round-trip and corpus isolation against a real pgvector-enabled PostgreSQL.
/// </summary>
/// <remarks>All fixtures are invented — no real document text or embedding (BR-12).</remarks>
[Collection("Aspire")]
public sealed class VectorStoreTests(AspireFixture aspire)
{
    private const string SyntheticModelId = "synthetic-embedding-model";

    [Fact]
    public async Task AChunkRoundTripsIncludingItsVectorColumn()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        Document document = await SeedDocumentAsync(context, Corpus.Personal, ct);
        float[] embedding = [0.1f, 0.2f, 0.3f, 0.4f];

        await using (WealthOpsDbContext write = context.CreateContext())
        {
            IVectorStore store = CreateStore(write);
            await store.UpsertChunksAsync(
                [Chunk(document, 0, "a synthetic passage", embedding)],
                ct);
        }

        await using WealthOpsDbContext read = context.CreateContext();
        DocumentChunk stored = await read.DocumentChunks.SingleAsync(ct);

        stored.Text.ShouldBe("a synthetic passage");
        stored.Corpus.ShouldBe(Corpus.Personal);
        stored.EmbeddingModelId.ShouldBe(SyntheticModelId);
        stored.Dimensions.ShouldBe(4);

        // The point of the test: the vector survives the Domain -> pgvector -> Domain conversion
        // intact, not merely the scalar columns beside it.
        stored.Embedding.ToArray().ShouldBe(embedding);
    }

    [Fact]
    public async Task SearchNeverReturnsAChunkFromAnotherCorpus()
    {
        // FR-M1-3 and the enforcement point behind BR-1. A rules query that could surface a
        // personal passage would erase the distinction the whole system is built to preserve.
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        Document personal = await SeedDocumentAsync(context, Corpus.Personal, ct);
        Document rules = await SeedDocumentAsync(context, Corpus.Rules, ct);

        float[] target = [1f, 0f, 0f, 0f];

        await using (WealthOpsDbContext write = context.CreateContext())
        {
            IVectorStore store = CreateStore(write);
            await store.UpsertChunksAsync(
            [
                // The personal chunk is the *closer* match, so a leak would be visible rather
                // than masked by ranking.
                Chunk(personal, 0, "a synthetic personal passage", target),
                Chunk(rules, 0, "a synthetic rules passage", [0f, 1f, 0f, 0f])
            ], ct);
        }

        await using WealthOpsDbContext read = context.CreateContext();
        IVectorStore search = CreateStore(read);

        IReadOnlyList<CorpusSearchResult> rulesHits = await search.SearchAsync(
            new CorpusQuery(Corpus.Rules, EmbeddingVector.Create(target), "a synthetic query"),
            limit: 10,
            ct);

        rulesHits.ShouldHaveSingleItem().DocumentId.ShouldBe(rules.Id);
        rulesHits[0].Text.ShouldBe("a synthetic rules passage");

        IReadOnlyList<CorpusSearchResult> personalHits = await search.SearchAsync(
            new CorpusQuery(Corpus.Personal, EmbeddingVector.Create(target), "a synthetic query"),
            limit: 10,
            ct);

        personalHits.ShouldHaveSingleItem().DocumentId.ShouldBe(personal.Id);
    }

    [Fact]
    public async Task SearchOrdersByCosineDistanceClosestFirst()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        Document document = await SeedDocumentAsync(context, Corpus.Rules, ct);

        await using (WealthOpsDbContext write = context.CreateContext())
        {
            await CreateStore(write).UpsertChunksAsync(
            [
                Chunk(document, 0, "orthogonal", [0f, 1f, 0f, 0f]),
                Chunk(document, 1, "identical", [1f, 0f, 0f, 0f]),
                Chunk(document, 2, "near", [0.9f, 0.1f, 0f, 0f])
            ], ct);
        }

        await using WealthOpsDbContext read = context.CreateContext();

        IReadOnlyList<CorpusSearchResult> hits = await CreateStore(read).SearchAsync(
            new CorpusQuery(Corpus.Rules, EmbeddingVector.Create([1f, 0f, 0f, 0f]), "a synthetic query"),
            limit: 3,
            ct);

        hits.Select(h => h.Text).ShouldBe(["identical", "near", "orthogonal"]);
        hits[0].Distance.ShouldBeLessThan(hits[1].Distance);
        hits[1].Distance.ShouldBeLessThan(hits[2].Distance);
    }

    [Fact]
    public async Task SearchRespectsItsLimit()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        Document document = await SeedDocumentAsync(context, Corpus.Rules, ct);

        await using (WealthOpsDbContext write = context.CreateContext())
        {
            await CreateStore(write).UpsertChunksAsync(
            [
                Chunk(document, 0, "first", [1f, 0f, 0f, 0f]),
                Chunk(document, 1, "second", [0.9f, 0.1f, 0f, 0f]),
                Chunk(document, 2, "third", [0.8f, 0.2f, 0f, 0f])
            ], ct);
        }

        await using WealthOpsDbContext read = context.CreateContext();

        IReadOnlyList<CorpusSearchResult> hits = await CreateStore(read).SearchAsync(
            new CorpusQuery(Corpus.Rules, EmbeddingVector.Create([1f, 0f, 0f, 0f]), "a synthetic query"),
            limit: 2,
            ct);

        hits.Count.ShouldBe(2);
    }

    [Fact]
    public async Task UpsertReplacesAChunkAtTheSameOrdinal()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        Document document = await SeedDocumentAsync(context, Corpus.Rules, ct);

        await using (WealthOpsDbContext first = context.CreateContext())
        {
            await CreateStore(first).UpsertChunksAsync(
                [Chunk(document, 0, "original text", [1f, 0f, 0f, 0f])], ct);
        }

        await using (WealthOpsDbContext second = context.CreateContext())
        {
            await CreateStore(second).UpsertChunksAsync(
                [Chunk(document, 0, "revised text", [0f, 1f, 0f, 0f])], ct);
        }

        await using WealthOpsDbContext read = context.CreateContext();

        // Re-chunking a changed document must replace, not accumulate a second set alongside.
        DocumentChunk stored = await read.DocumentChunks.SingleAsync(ct);
        stored.Text.ShouldBe("revised text");
    }

    [Fact]
    public async Task AChunkWhoseWidthDisagreesWithConfigurationIsRejected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        Document document = await SeedDocumentAsync(context, Corpus.Rules, ct);

        await using WealthOpsDbContext write = context.CreateContext();
        IVectorStore store = CreateStore(write);

        await Should.ThrowAsync<EmbeddingDimensionMismatchException>(async () =>
            await store.UpsertChunksAsync(
                [Chunk(document, 0, "a synthetic passage", [0.1f, 0.2f])],
                ct));

        // Rejected before anything was written, so a bad batch fails whole rather than half.
        await using WealthOpsDbContext read = context.CreateContext();
        (await read.DocumentChunks.CountAsync(ct)).ShouldBe(0);
    }

    [Fact]
    public async Task AMismatchedBatchWritesNothingAtAll()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        Document document = await SeedDocumentAsync(context, Corpus.Rules, ct);

        await using WealthOpsDbContext write = context.CreateContext();

        await Should.ThrowAsync<EmbeddingDimensionMismatchException>(async () =>
            await CreateStore(write).UpsertChunksAsync(
            [
                Chunk(document, 0, "valid", [0.1f, 0.2f, 0.3f, 0.4f]),
                Chunk(document, 1, "invalid", [0.1f, 0.2f])
            ], ct));

        await using WealthOpsDbContext read = context.CreateContext();
        (await read.DocumentChunks.CountAsync(ct)).ShouldBe(0);
    }

    [Fact]
    public async Task AQueryVectorOfTheWrongWidthIsRejected()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        await using WealthOpsDbContext read = context.CreateContext();

        await Should.ThrowAsync<EmbeddingDimensionMismatchException>(async () =>
            await CreateStore(read).SearchAsync(
                new CorpusQuery(Corpus.Rules, EmbeddingVector.Create([1f, 0f]), "a synthetic query"),
                limit: 5,
                ct));
    }

    [Fact]
    public async Task EmbeddingProfilesReportWhatIsActuallyStored()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        Document document = await SeedDocumentAsync(context, Corpus.Rules, ct);

        await using (WealthOpsDbContext write = context.CreateContext())
        {
            await CreateStore(write).UpsertChunksAsync(
            [
                Chunk(document, 0, "first", [1f, 0f, 0f, 0f]),
                Chunk(document, 1, "second", [0f, 1f, 0f, 0f])
            ], ct);
        }

        await using WealthOpsDbContext read = context.CreateContext();

        // What status compares against configuration to detect a silent model change (ADR-002).
        EmbeddingProfile profile = (await CreateStore(read).GetEmbeddingProfilesAsync(ct)).ShouldHaveSingleItem();
        profile.EmbeddingModelId.ShouldBe(SyntheticModelId);
        profile.Dimensions.ShouldBe(4);
        profile.ChunkCount.ShouldBe(2);
    }

    [Fact]
    public async Task AnEmptyBatchIsANoOp()
    {
        CancellationToken ct = TestContext.Current.CancellationToken;
        await using PersistenceTestContext context = await PersistenceTestContext.CreateAsync(aspire, ct);

        await using WealthOpsDbContext write = context.CreateContext();
        await CreateStore(write).UpsertChunksAsync([], ct);

        (await write.DocumentChunks.CountAsync(ct)).ShouldBe(0);
    }

    private static VectorStore CreateStore(WealthOpsDbContext dbContext)
        => new(dbContext, PersistenceTestContext.EmbeddingModel(), NullLogger<VectorStore>.Instance);

    private static DocumentChunk Chunk(Document document, int ordinal, string text, float[] embedding)
        => DocumentChunk.Create(document, ordinal, text, EmbeddingVector.Create(embedding), SyntheticModelId);

    private static async Task<Document> SeedDocumentAsync(
        PersistenceTestContext context,
        Corpus corpus,
        CancellationToken cancellationToken)
    {
        var document = Document.Create(
            Path.Combine(Path.GetTempPath(), "wealthops-synthetic", $"{corpus}-{Guid.NewGuid():N}.txt"),
            corpus == Corpus.Rules ? DocumentType.TaxRulePage : DocumentType.Payslip,
            ContentHash.FromBytes(Guid.NewGuid().ToByteArray()),
            corpus,
            new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero));

        await using WealthOpsDbContext dbContext = context.CreateContext();
        dbContext.Documents.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);

        return document;
    }
}
