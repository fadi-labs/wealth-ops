using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WealthOps.Application.Common.Clients;
using WealthOps.Application.Common.Exceptions;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Infrastructure.Clients.OpenAiCompatible;

/// <summary>
/// Calls <c>POST /embeddings</c> on the configured endpoint.
/// </summary>
/// <remarks>
/// Verifies that what comes back is the width configuration declared, and refuses it otherwise.
/// That check is the reason the class exists rather than being a thin JSON call: a vector of the
/// wrong width still stores, still searches, and still returns plausible neighbours — the damage
/// is silent and permanent (ADR-002, NFR-7).
/// </remarks>
internal sealed class EmbeddingsClient(
    HttpClient httpClient,
    IOptions<ModelGatewayOptions> options,
    ILogger<EmbeddingsClient> logger)
    : IEmbeddingModel
{
    private readonly ModelGatewayOptions _options = options.Value;

    public string ModelId => _options.Embedding.Id;

    public int Dimensions => _options.Embedding.Dimensions;

    public async Task<EmbeddingVector> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        IReadOnlyList<EmbeddingVector> vectors = await EmbedAsync([text], cancellationToken);
        return vectors[0];
    }

    public async Task<IReadOnlyList<EmbeddingVector>> EmbedAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        if (texts.Count == 0)
        {
            return [];
        }

        var body = new EmbeddingRequestBody { Model = ModelId, Input = texts };

        // Counts only — the texts themselves may be personal content (NFR-8).
        logger.LogDebug("Requesting embeddings. Inputs: {InputCount}", texts.Count);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync("embeddings", body, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ModelGatewayException(
                $"The model endpoint at '{httpClient.BaseAddress}' did not respond to an embedding request. " +
                "Confirm the local inference process is running.",
                ex);
        }

        using (response)
        {
            await ChatCompletionsClient.EnsureSuccessAsync(response, cancellationToken);

            EmbeddingResponseBody? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<EmbeddingResponseBody>(cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new ModelGatewayException(
                    "The model endpoint returned an embedding payload that could not be read.",
                    ex);
            }

            IReadOnlyList<EmbeddingDataBody> data = payload?.Data ?? [];

            if (data.Count != texts.Count)
            {
                throw new ModelGatewayException(
                    $"Requested {texts.Count} embeddings but the endpoint returned {data.Count}. " +
                    "Pairing them up by position would silently attach vectors to the wrong text.");
            }

            // The protocol carries an index per item and does not promise request order.
            EmbeddingDataBody[] ordered = [.. data.OrderBy(d => d.Index)];
            var vectors = new EmbeddingVector[ordered.Length];

            for (int i = 0; i < ordered.Length; i++)
            {
                float[] values = ordered[i].Embedding
                    ?? throw new ModelGatewayException(
                        $"The endpoint returned an embedding entry at index {i} with no vector.");

                if (values.Length != Dimensions)
                {
                    throw new EmbeddingDimensionMismatchException(ModelId, Dimensions, values.Length);
                }

                vectors[i] = EmbeddingVector.Create(values);
            }

            logger.LogDebug("Embeddings received. Vectors: {VectorCount}", vectors.Length);

            return vectors;
        }
    }
}
