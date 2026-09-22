using WealthOps.Domain.Entities;
using WealthOps.Domain.Enums;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Domain.UnitTest.Entities;

public sealed class DocumentChunkTests
{
    private const string SyntheticModelId = "synthetic-embedding-model";

    [Theory]
    [InlineData(Corpus.Personal)]
    [InlineData(Corpus.Rules)]
    public void Create_InheritsCorpusAndDocumentIdFromItsDocument(Corpus corpus)
    {
        Document document = CreateDocument(corpus);

        DocumentChunk chunk = DocumentChunk.Create(
            document,
            ordinal: 0,
            text: "synthetic passage",
            embedding: EmbeddingVector.Create([0.1f, 0.2f, 0.3f]),
            embeddingModelId: SyntheticModelId);

        // There is no overload that lets a caller pair a chunk with the wrong corpus — which is
        // what makes corpus-scoped search (FR-M1-3) an invariant rather than a convention.
        chunk.Corpus.ShouldBe(corpus);
        chunk.DocumentId.ShouldBe(document.Id);
    }

    [Fact]
    public void Create_RecordsTheModelAndWidthOfItsEmbedding()
    {
        DocumentChunk chunk = DocumentChunk.Create(
            CreateDocument(Corpus.Rules),
            ordinal: 3,
            text: "synthetic passage",
            embedding: EmbeddingVector.Create([0.1f, 0.2f, 0.3f, 0.4f, 0.5f]),
            embeddingModelId: SyntheticModelId);

        // FR-M1-6: vectors from different models are not comparable, so each one records its own.
        chunk.EmbeddingModelId.ShouldBe(SyntheticModelId);
        chunk.Dimensions.ShouldBe(5);
        chunk.Ordinal.ShouldBe(3);
    }

    [Fact]
    public void Create_RejectsANegativeOrdinal()
        => Should.Throw<ArgumentOutOfRangeException>(() => DocumentChunk.Create(
            CreateDocument(Corpus.Rules),
            ordinal: -1,
            text: "synthetic passage",
            embedding: EmbeddingVector.Create([0.1f]),
            embeddingModelId: SyntheticModelId));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsBlankText(string text)
        => Should.Throw<ArgumentException>(() => DocumentChunk.Create(
            CreateDocument(Corpus.Rules),
            ordinal: 0,
            text: text,
            embedding: EmbeddingVector.Create([0.1f]),
            embeddingModelId: SyntheticModelId));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsABlankModelIdentifier(string modelId)
        => Should.Throw<ArgumentException>(() => DocumentChunk.Create(
            CreateDocument(Corpus.Rules),
            ordinal: 0,
            text: "synthetic passage",
            embedding: EmbeddingVector.Create([0.1f]),
            embeddingModelId: modelId));

    [Fact]
    public void Create_RejectsANullDocument()
        => Should.Throw<ArgumentNullException>(() => DocumentChunk.Create(
            document: null!,
            ordinal: 0,
            text: "synthetic passage",
            embedding: EmbeddingVector.Create([0.1f]),
            embeddingModelId: SyntheticModelId));

    private static Document CreateDocument(Corpus corpus) => Document.Create(
        Path.Combine(Path.GetTempPath(), "wealthops-synthetic", "sample.txt"),
        corpus == Corpus.Rules ? DocumentType.TaxRulePage : DocumentType.Payslip,
        ContentHash.FromBytes("synthetic fixture content"u8),
        corpus,
        new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero));
}
