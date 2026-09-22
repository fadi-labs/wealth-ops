using Microsoft.Extensions.Logging.Abstractions;
using WealthOps.Application.Common.Clients;
using WealthOps.Application.Features.Chat;

namespace WealthOps.Application.UnitTest.Features.Chat;

/// <summary>
/// The chat model is stubbed throughout — no automated test may require a running language
/// model (NFR-3, AC-9).
/// </summary>
public sealed class SendChatMessageTests
{
    [Fact]
    public async Task TheNewUserTurnIsAppendedToTheHistorySentToTheModel()
    {
        var model = new StubChatModel("a synthetic reply");
        var handler = new SendChatMessage.Handler(model, NullLogger<SendChatMessage.Handler>.Instance);

        await handler.Handle(
            new SendChatMessage.Request([ChatMessage.User("earlier turn")], "latest turn"),
            TestContext.Current.CancellationToken);

        ChatCompletionRequest sent = model.LastRequest.ShouldNotBeNull();
        sent.Messages.Count.ShouldBe(2);
        sent.Messages[^1].Role.ShouldBe(ChatRole.User);
        sent.Messages[^1].Content.ShouldBe("latest turn");
    }

    [Fact]
    public async Task TheReturnedConversationCarriesBothNewTurns()
    {
        // The CLI feeds this straight back on the next turn, so dropping either one would
        // silently truncate the conversation.
        var model = new StubChatModel("a synthetic reply");
        var handler = new SendChatMessage.Handler(model, NullLogger<SendChatMessage.Handler>.Instance);

        SendChatMessage.Response response = await handler.Handle(
            new SendChatMessage.Request([], "first turn"),
            TestContext.Current.CancellationToken);

        response.Conversation.Count.ShouldBe(2);
        response.Conversation[0].ShouldBe(ChatMessage.User("first turn"));
        response.Conversation[1].ShouldBe(ChatMessage.Assistant("a synthetic reply"));
        response.Reply.ShouldBe("a synthetic reply");
    }

    [Fact]
    public async Task M0SendsNoToolDefinitions()
    {
        // Tool-backed calculation arrives in M3 (FR-M3-9). Offering a tool the system cannot
        // service would invite the model to fabricate a call.
        var model = new StubChatModel("a synthetic reply");
        var handler = new SendChatMessage.Handler(model, NullLogger<SendChatMessage.Handler>.Instance);

        await handler.Handle(
            new SendChatMessage.Request([], "a question"),
            TestContext.Current.CancellationToken);

        (model.LastRequest!.Tools ?? []).ShouldBeEmpty();
    }

    [Fact]
    public void AnEmptyMessageIsRejected()
    {
        var validator = new SendChatMessage.Validator();

        validator.Validate(new SendChatMessage.Request([], "")).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void AMessageWithEmptyHistoryIsValid()
    {
        var validator = new SendChatMessage.Validator();

        validator.Validate(new SendChatMessage.Request([], "a question")).IsValid.ShouldBeTrue();
    }

    private sealed class StubChatModel(string reply) : IChatModel
    {
        public string ModelId => "synthetic-chat-model";

        public ChatCompletionRequest? LastRequest { get; private set; }

        public Task<ChatCompletion> CompleteAsync(
            ChatCompletionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new ChatCompletion(reply, [], ModelId));
        }
    }
}
