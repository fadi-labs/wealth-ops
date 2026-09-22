using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WealthOps.Application.Common.Clients;
using WealthOps.Infrastructure.Clients.OpenAiCompatible;

namespace WealthOps.Infrastructure.UnitTest.Clients.OpenAiCompatible;

/// <summary>
/// The probe must report failure as data, never throw. <c>status</c> is the instrument an
/// operator reaches for when the stack is broken.
/// </summary>
public sealed class ModelEndpointProbeTests
{
    [Fact]
    public async Task AReachableEndpointReportsItsAdvertisedModels()
    {
        var handler = StubHttpMessageHandler.RespondingWithJson(
            """
            {
              "data": [
                { "id": "synthetic-embedding-model" },
                { "id": "synthetic-chat-model" }
              ]
            }
            """);

        ModelEndpointStatus status = await CreateProbe(handler).ProbeAsync(TestContext.Current.CancellationToken);

        status.IsReachable.ShouldBeTrue();
        status.FailureReason.ShouldBeNull();
        status.AvailableModelIds.ShouldBe(["synthetic-chat-model", "synthetic-embedding-model"]);
    }

    [Fact]
    public async Task AnUnreachableEndpointIsReportedNotThrown()
    {
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("connection refused"));

        ModelEndpointStatus status = await CreateProbe(handler).ProbeAsync(TestContext.Current.CancellationToken);

        status.IsReachable.ShouldBeFalse();
        status.FailureReason.ShouldNotBeNullOrWhiteSpace();
        status.AvailableModelIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task ANonSuccessStatusIsReportedNotThrown()
    {
        var handler = StubHttpMessageHandler.RespondingWith(HttpStatusCode.ServiceUnavailable);

        ModelEndpointStatus status = await CreateProbe(handler).ProbeAsync(TestContext.Current.CancellationToken);

        status.IsReachable.ShouldBeFalse();
        status.FailureReason.ShouldNotBeNull().ShouldContain("503");
    }

    [Fact]
    public async Task AMalformedPayloadIsReportedNotThrown()
    {
        var handler = StubHttpMessageHandler.RespondingWithJson("this is not json");

        ModelEndpointStatus status = await CreateProbe(handler).ProbeAsync(TestContext.Current.CancellationToken);

        status.IsReachable.ShouldBeFalse();
    }

    [Fact]
    public async Task AnEndpointThatListsNoModelsIsStillReachable()
    {
        // Not every OpenAI-compatible server implements a model listing. Absence of a listing is
        // not evidence that a model is missing.
        var handler = StubHttpMessageHandler.RespondingWithJson("""{ "data": [] }""");

        ModelEndpointStatus status = await CreateProbe(handler).ProbeAsync(TestContext.Current.CancellationToken);

        status.IsReachable.ShouldBeTrue();
        status.AvailableModelIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheConfiguredEndpointIsEchoedBack()
    {
        var handler = StubHttpMessageHandler.RespondingWithJson("""{ "data": [] }""");

        ModelEndpointStatus status = await CreateProbe(handler).ProbeAsync(TestContext.Current.CancellationToken);

        status.Endpoint.ShouldBe(new Uri(GatewayTestOptions.Create().Endpoint));
    }

    private static ModelEndpointProbe CreateProbe(StubHttpMessageHandler handler)
    {
        ModelGatewayOptions options = GatewayTestOptions.Create();

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.Endpoint + "/")
        };

        return new ModelEndpointProbe(
            httpClient,
            Options.Create(options),
            NullLogger<ModelEndpointProbe>.Instance);
    }
}
