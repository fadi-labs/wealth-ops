namespace WealthOps.Application.Common.Clients;

/// <summary>
/// The conversational model, addressed through whatever endpoint is configured (EP-3).
/// </summary>
/// <remarks>
/// Application depends on this interface and never on a concrete client, which is what makes
/// "switching the chat model is a configuration edit with no rebuild" (AC-12) true rather than
/// aspirational. No implementation of this interface may transmit personal content anywhere but
/// the configured local endpoint (BR-1, NFR-1).
/// </remarks>
public interface IChatModel
{
    /// <summary>The configured model identifier, for reporting by <c>status</c>.</summary>
    string ModelId { get; }

    /// <exception cref="Exceptions.ModelGatewayException">
    /// The endpoint was unreachable, returned a non-success status, or returned a payload that
    /// could not be read as a completion.
    /// </exception>
    Task<ChatCompletion> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default);
}
