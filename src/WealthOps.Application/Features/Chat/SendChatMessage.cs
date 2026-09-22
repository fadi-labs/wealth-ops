using FluentValidation;
using Mediator;
using Microsoft.Extensions.Logging;
using WealthOps.Application.Common.Clients;

namespace WealthOps.Application.Features.Chat;

/// <summary>
/// Sends one conversational turn to the configured chat model and returns its reply.
/// </summary>
/// <remarks>
/// M0 scope: no tools and no retrieval. Retrieval lands in M1 (FR-M1-5) and tool-backed
/// calculation in M3 (FR-M3-9); until then the model answers from its own knowledge, which is
/// exactly why it must not be asked anything numeric about the operator's position (BR-2).
/// </remarks>
public static class SendChatMessage
{
    /// <param name="History">The conversation so far, oldest first. Excludes <paramref name="Message"/>.</param>
    /// <param name="Message">The operator's new turn.</param>
    public sealed record Request(IReadOnlyList<ChatMessage> History, string Message)
        : IRequest<Response>;

    /// <param name="Reply">The assistant's turn.</param>
    /// <param name="ModelId">The model that answered, as reported by the endpoint.</param>
    /// <param name="Conversation">The full conversation including both new turns, for the next call.</param>
    public sealed record Response(
        string Reply,
        string ModelId,
        IReadOnlyList<ChatMessage> Conversation);

    public sealed class Validator : AbstractValidator<Request>
    {
        public Validator()
        {
            RuleFor(r => r.Message)
                .NotEmpty()
                .WithMessage("A chat message cannot be empty.");

            RuleFor(r => r.History)
                .NotNull()
                .WithMessage("Conversation history must be supplied, even when empty.");
        }
    }

    public sealed class Handler(IChatModel chatModel, ILogger<Handler> logger)
        : IRequestHandler<Request, Response>
    {
        public async ValueTask<Response> Handle(Request request, CancellationToken cancellationToken)
        {
            // Turn counts and durations only — never the content of a turn (NFR-8).
            logger.LogInformation("Chat completion started. Turns in history: {TurnCount}", request.History.Count);

            List<ChatMessage> messages = [.. request.History, ChatMessage.User(request.Message)];

            ChatCompletion completion = await chatModel.CompleteAsync(
                new ChatCompletionRequest(messages),
                cancellationToken);

            List<ChatMessage> conversation = [.. messages, ChatMessage.Assistant(completion.Content)];

            logger.LogInformation(
                "Chat completion completed. Turns in conversation: {TurnCount}",
                conversation.Count);

            return new Response(completion.Content, completion.ModelId, conversation);
        }
    }
}
