using WealthOps.Domain.ValueObjects;

namespace WealthOps.Application.Common.Clients;

/// <summary>
/// The embedding model, addressed through whatever endpoint is configured (EP-3).
/// </summary>
/// <remarks>
/// <see cref="ModelId"/> and <see cref="Dimensions"/> are on the contract rather than read from
/// options by callers, so that nothing outside Infrastructure has to know a model identifier —
/// AC-12 and BR-13 both require that no model name appear in code.
/// </remarks>
public interface IEmbeddingModel
{
    /// <summary>The configured embedding model identifier, recorded on every chunk (FR-M1-6).</summary>
    string ModelId { get; }

    /// <summary>
    /// The dimension the configured model is declared to produce.
    /// </summary>
    /// <remarks>
    /// Declared, not discovered: it comes from configuration. An implementation must verify that
    /// what the endpoint actually returns matches, and fail loudly when it does not (ADR-002) —
    /// a quietly mismatched vector is unrecoverable once stored.
    /// </remarks>
    int Dimensions { get; }

    /// <exception cref="Exceptions.ModelGatewayException">
    /// The endpoint failed, or returned a vector whose width is not <see cref="Dimensions"/>.
    /// </exception>
    Task<EmbeddingVector> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Embeds several texts in one request.
    /// </summary>
    /// <returns>Vectors in the same order as <paramref name="texts"/>.</returns>
    /// <exception cref="Exceptions.ModelGatewayException">
    /// The endpoint failed, returned a different number of vectors than texts supplied, or
    /// returned a vector whose width is not <see cref="Dimensions"/>.
    /// </exception>
    Task<IReadOnlyList<EmbeddingVector>> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}
