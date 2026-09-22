using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WealthOps.Application.Common.Clients;
using WealthOps.Application.Common.Exceptions;
using WealthOps.Infrastructure.Clients.OpenAiCompatible;

namespace WealthOps.Infrastructure.UnitTest.Clients.OpenAiCompatible;

public sealed class ChatCompletionsClientTests
{
    [Fact]
    public async Task ASuccessfulCompletionIsReturned()
    {
        var handler = StubHttpMessageHandler.RespondingWithJson(
            """
            {
              "model": "synthetic-chat-model",
              "choices": [ { "message": { "content": "a synthetic reply" } } ]
            }
            """);

        ChatCompletion completion = await CreateClient(handler).CompleteAsync(
            new ChatCompletionRequest([ChatMessage.User("a question")]),
            TestContext.Current.CancellationToken);

        completion.Content.ShouldBe("a synthetic reply");
        completion.ModelId.ShouldBe("synthetic-chat-model");
        completion.ToolCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheConfiguredModelIdIsSentAndTheRelativePathPreservesTheEndpointPath()
    {
        var handler = StubHttpMessageHandler.RespondingWithJson(
            """{ "choices": [ { "message": { "content": "ok" } } ] }""");

        await CreateClient(handler).CompleteAsync(
            new ChatCompletionRequest([ChatMessage.User("a question")]),
            TestContext.Current.CancellationToken);

        // The trailing-slash handling in the client matters: without it an endpoint configured
        // as ".../v1" silently loses the "/v1" segment.
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v1/chat/completions");
        handler.RequestBodies[0].ShouldContain("synthetic-chat-model");
    }

    [Fact]
    public async Task ToolCallsAreSurfaced()
    {
        var handler = StubHttpMessageHandler.RespondingWithJson(
            """
            {
              "choices": [ {
                "message": {
                  "content": "",
                  "tool_calls": [ {
                    "id": "call-1",
                    "function": { "name": "synthetic_tool", "arguments": "{\"year\":2026}" }
                  } ]
                }
              } ]
            }
            """);

        ChatCompletion completion = await CreateClient(handler).CompleteAsync(
            new ChatCompletionRequest([ChatMessage.User("a question")]),
            TestContext.Current.CancellationToken);

        ChatToolCall call = completion.ToolCalls.ShouldHaveSingleItem();
        call.Id.ShouldBe("call-1");
        call.Name.ShouldBe("synthetic_tool");
        call.ArgumentsJson.ShouldContain("2026");
    }

    [Fact]
    public async Task ANonSuccessStatusBecomesAGatewayExceptionCarryingTheEndpointDetail()
    {
        var handler = StubHttpMessageHandler.RespondingWith(
            HttpStatusCode.NotFound,
            "model 'synthetic-chat-model' not found");

        ModelGatewayException exception = await Should.ThrowAsync<ModelGatewayException>(
            async () => await CreateClient(handler).CompleteAsync(
                new ChatCompletionRequest([ChatMessage.User("a question")]),
                TestContext.Current.CancellationToken));

        // The endpoint's own text is usually the only actionable part — an unknown model name is
        // reported there and nowhere else.
        exception.Message.ShouldContain("404");
        exception.Message.ShouldContain("not found");
    }

    [Fact]
    public async Task AnUnreachableEndpointBecomesAnActionableGatewayException()
    {
        var handler = StubHttpMessageHandler.Throwing(new HttpRequestException("connection refused"));

        ModelGatewayException exception = await Should.ThrowAsync<ModelGatewayException>(
            async () => await CreateClient(handler).CompleteAsync(
                new ChatCompletionRequest([ChatMessage.User("a question")]),
                TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("did not respond");
        exception.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task AResponseWithNoChoicesIsRejected()
    {
        var handler = StubHttpMessageHandler.RespondingWithJson("""{ "choices": [] }""");

        ModelGatewayException exception = await Should.ThrowAsync<ModelGatewayException>(
            async () => await CreateClient(handler).CompleteAsync(
                new ChatCompletionRequest([ChatMessage.User("a question")]),
                TestContext.Current.CancellationToken));

        exception.Message.ShouldContain("no completion choices");
    }

    [Fact]
    public async Task AMalformedPayloadIsRejected()
    {
        var handler = StubHttpMessageHandler.RespondingWithJson("this is not json");

        await Should.ThrowAsync<ModelGatewayException>(
            async () => await CreateClient(handler).CompleteAsync(
                new ChatCompletionRequest([ChatMessage.User("a question")]),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheClientReportsTheConfiguredModelId()
        => CreateClient(StubHttpMessageHandler.RespondingWithJson("{}")).ModelId
            .ShouldBe("synthetic-chat-model");

    private static ChatCompletionsClient CreateClient(StubHttpMessageHandler handler)
    {
        ModelGatewayOptions options = GatewayTestOptions.Create();

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(options.Endpoint + "/")
        };

        return new ChatCompletionsClient(
            httpClient,
            Options.Create(options),
            NullLogger<ChatCompletionsClient>.Instance);
    }
}
