namespace WealthOps.Application.Common.Exceptions;

/// <summary>
/// A vector's width does not match the configured embedding model's.
/// </summary>
/// <remarks>
/// This is a loud failure by design (ADR-002, NFR-7). The tempting alternatives — truncate, pad,
/// or store it anyway — all produce a vector that still returns plausible neighbours while being
/// meaningless, and the damage is not detectable after the fact. Refusing the write is the only
/// recoverable option.
/// </remarks>
public sealed class EmbeddingDimensionMismatchException : Exception
{
    public EmbeddingDimensionMismatchException(
        string embeddingModelId,
        int expectedDimensions,
        int actualDimensions)
        : base($"Embedding model '{embeddingModelId}' is configured for {expectedDimensions} dimensions " +
               $"but the vector has {actualDimensions}. Re-embed the affected corpus, or correct " +
               "WealthOps:Models:Embedding:Dimensions to match the model actually in use.")
    {
        EmbeddingModelId = embeddingModelId;
        ExpectedDimensions = expectedDimensions;
        ActualDimensions = actualDimensions;
    }

    public string EmbeddingModelId { get; }

    public int ExpectedDimensions { get; }

    public int ActualDimensions { get; }
}
