using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WealthOps.Application.Common.Exceptions;
using WealthOps.Domain.ValueObjects;
using WealthOps.Infrastructure.Clients.OpenAiCompatible;

namespace WealthOps.Infrastructure.UnitTest.Clients.OpenAiCompatible;

public sealed class EmbeddingsClientTests
{
    [Fact]
    public async Task ASingleEmbeddingIsReturned()
    {
        var handler = StubHttpMessageHandler.RespondingWithJson(
            """{ "data": [ { "index": 0, "embedding": [0.1, 0.2, 0.3, 0.4] } ] }""");

        EmbeddingVector vector = await CreateClient(handler).EmbedAsync(
            "a synthetic passage",
            TestContext.Current.CancellationToken);

        vector.Dimensions.ShouldBe(4);
        vector.ToArray().ShouldBe([0.1f, 0.2f, 0.3f, 0.4f]);
    }

    [Fact]
    public async Task VectorsAreOrderedByTheIndexTheEndpointReports()
    {
        // The protocol carries an index per item and does not promise request order. Pairing by
        // arrival would silently attach each vector to the wrong text.
        var handler = StubHttpMessageHandler.RespondingWithJson(
            """
            {
              "data": [
                { "index": 1, "embedding": [0.9, 0.9, 0.9, 0.9] },
                { "index": 0, "embedding": [0.1, 0.1, 0.1, 0.1] }
              ]
            }
            """);

        IReadOnlyList<EmbeddingVector> vectors = await CreateClient(handler).EmbedAsync(
            ["first", "second"],
            TestContext.Current.CancellationToken);

        vectors[0].ToArray()[0].ShouldBe(0.1f);
        vectors[1].ToArray()[0].ShouldBe(0.9f);
    }

    [Fact]
    public async Task AWidthThatDisagreesWithConfigurationIsRejected()
    {
        // The central guarantee of ADR-002. A three-wide vector stored against a four-wide
        // configuration still searches and still returns plausible neighbours — the damage is
        // silent, so the write must not happen.
        var handler = StubHttpMessageHandler.RespondingWithJson(
            """{ "data": [ { "index": 0, "embedding": [0.1, 0.2, 0.3] } ] }""");

        EmbeddingDimensionMismatchException exception =
            await Should.ThrowAsync<EmbeddingDimensionMismatchException>(
                async () => await CreateClient(handler).EmbedAsync(
                    "a synthetic passage",
                    TestContext.Current.CancellationToken));

        exception.ExpectedDimensions.ShouldBe(4);
        exception.ActualDimensions.ShouldBe(3);
        exception.EmbeddingModelId.ShouldBe(GatewayTestOptions.EmbeddingModelId);
    }

    [Fact]
    public async Task AWrongNumberOfVectorsIsRejected()
    {
        var handler = StubHttpMessageHandler.RespondingWithJson(
            """{ "data": [ { "index": 0, "embedding": [0.1, 0.2, 0.3, 0.4] } ] }""");

        ModelGatewayException exception = await Should.ThrowAsync<ModelGatewayException>(
            async () => await CreateClient(handler).EmbedAsync(
                ["first", "second"],
                TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("Requested 2 embeddings");
    }

    [Fact]
    public async Task AnEmptyDataArrayIsRejected()
    {
        var handler = StubHttpMessageHandler.RespondingWithJson("""{ "data": [] }""");

        await Should.ThrowAsync<ModelGatewayException>(
            async () => await CreateClient(handler).EmbedAsync(
                "a synthetic passage",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnEmptyInputListSkipsTheCallEntirely()
    {
        var handler = StubHttpMessageHandler.RespondingWithJson("""{ "data": [] }""");

        IReadOnlyList<EmbeddingVector> vectors = await CreateClient(handler).EmbedAsync(
            [],
            TestContext.Current.CancellationToken);

        vectors.ShouldBeEmpty();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ANonSuccessStatusBecomesAGatewayException()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.InternalServerError, "boom");

        await Should.ThrowAsync<ModelGatewayException>(
            async () => await CreateClient(handler).EmbedAsync(
                "a synthetic passage",
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AnUnreachableEndpointBecomesAnActionableGatewayException()
    {
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("connection refused"));

        ModelGatewayException exception = await Should.ThrowAsync<ModelGatewayException>(
            async () => await CreateClient(handler).EmbedAsync(
                "a synthetic passage",
                TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("did not respond");
    }

    [Fact]
    public void TheClientReportsItsConfiguredModelAndWidth()
    {
        EmbeddingsClient client = CreateClient(StubHttpMessageHandler.RespondingWithJson("{}"));

        // Exposed on the contract so nothing outside Infrastructure needs to know a model name.
        client.ModelId.ShouldBe(GatewayTestOptions.EmbeddingModelId);
        client.Dimensions.ShouldBe(4);
    }

    private static EmbeddingsClient CreateClient(StubHttpMessageHandler handler)
    {
        ModelGatewayOptions options = GatewayTestOptions.Create();

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.Endpoint + "/")
        };

        return new EmbeddingsClient(
            httpClient,
            Options.Create(options),
            NullLogger<EmbeddingsClient>.Instance);
    }
}
