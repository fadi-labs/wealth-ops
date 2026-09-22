using Mediator;
using WealthOps.Application.Common.Clients;
using WealthOps.Application.Common.Exceptions;
using WealthOps.Application.Features.Chat;

namespace WealthOps.Cli.Commands;

/// <summary>
/// An interactive conversation with the configured chat model.
/// </summary>
/// <remarks>
/// <para>
/// M0 has no retrieval and no tools, so the model answers from its own knowledge. That is fine for
/// general questions and dangerous for specific ones, which is why the banner says so: until M1
/// and M3 land, nothing here is grounded in the operator's documents and no figure it produces is
/// trustworthy (BR-2, BR-10).
/// </para>
/// <para>
/// A gateway failure ends the turn, not the session. The likeliest cause is that the local
/// inference process stopped, and losing an entire conversation to a restart would be a poor
/// trade.
/// </para>
/// </remarks>
internal static class ChatCommand
{
    private const string ExitCommand = "/exit";
    private const string ResetCommand = "/reset";

    public static async Task<int> RunAsync(
        IMediator mediator,
        TextReader input,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        output.WriteLine("WealthOps chat. Type /exit to leave, /reset to start a fresh conversation.");
        output.WriteLine();
        output.WriteLine("M0: no retrieval and no calculation tools are wired yet, so answers are not");
        output.WriteLine("grounded in your documents and any figure is the model's own invention.");
        output.WriteLine("Ask about concepts, not about your position.");
        output.WriteLine();

        List<ChatMessage> conversation = [];

        while (!cancellationToken.IsCancellationRequested)
        {
            output.Write("> ");
            output.Flush();

            string? line = await input.ReadLineAsync(cancellationToken);

            if (line is null || string.Equals(line.Trim(), ExitCommand, StringComparison.OrdinalIgnoreCase))
            {
                return ExitCodes.Success;
            }

            string message = line.Trim();

            if (message.Length == 0)
            {
                continue;
            }

            if (string.Equals(message, ResetCommand, StringComparison.OrdinalIgnoreCase))
            {
                conversation.Clear();
                output.WriteLine("Conversation reset.");
                output.WriteLine();
                continue;
            }

            try
            {
                SendChatMessage.Response response = await mediator.Send(
                    new SendChatMessage.Request(conversation, message),
                    cancellationToken);

                conversation = [.. response.Conversation];

                output.WriteLine();
                output.WriteLine(response.Reply);
                output.WriteLine();
            }
            catch (ModelGatewayException ex)
            {
                output.WriteLine();
                output.WriteLine($"[!] {ex.Message}");
                output.WriteLine("    The conversation is intact — try again once the endpoint is back.");
                output.WriteLine();
            }
        }

        return ExitCodes.Success;
    }
}
