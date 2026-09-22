using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WealthOps.Application.Common.Clients;
using WealthOps.Application.Common.Exceptions;

namespace WealthOps.Infrastructure.Clients.OpenAiCompatible;

/// <summary>
/// Calls <c>POST /chat/completions</c> on the configured endpoint.
/// </summary>
/// <remarks>
/// Named for the protocol endpoint it speaks to, not for a vendor. Any server implementing this
/// shape is addressable by configuration alone, which is the whole of EP-3.
/// </remarks>
internal sealed class ChatCompletionsClient(
    HttpClient httpClient,
    IOptions<ModelGatewayOptions> options,
    ILogger<ChatCompletionsClient> logger)
    : IChatModel
{
    private readonly ModelGatewayOptions _options = options.Value;

    public string ModelId => _options.Chat.Id;

    public async Task<ChatCompletion> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var body = new ChatCompletionRequestBody
        {
            Model = ModelId,
            Messages = [.. request.Messages.Select(ToWire)],
            Tools = request.Tools is { Count: > 0 } tools ? [.. tools.Select(ToWire)] : null,
            Temperature = request.Temperature
        };

        // Message count only — never message content (NFR-8).
        logger.LogDebug(
            "Requesting completion. Messages: {MessageCount}. Tools offered: {ToolCount}",
            body.Messages.Count,
            body.Tools?.Count ?? 0);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync("chat/completions", body, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ModelGatewayException(
                $"The model endpoint at '{httpClient.BaseAddress}' did not respond. " +
                "Confirm the local inference process is running and that WealthOps:Models:Endpoint points at it.",
                ex);
        }

        using (response)
        {
            await EnsureSuccessAsync(response, cancellationToken);

            ChatCompletionResponseBody? payload;
            try
            {
                payload = await response.Content.ReadFromJsonAsync<ChatCompletionResponseBody>(cancellationToken);
            }
            catch (JsonException ex)
            {
                throw new ModelGatewayException(
                    "The model endpoint returned a chat completion payload that could not be read.",
                    ex);
            }

            ResponseMessageBody message =
                payload?.Choices?.FirstOrDefault()?.Message
                ?? throw new ModelGatewayException(
                    "The model endpoint returned no completion choices. " +
                    $"Confirm the model '{ModelId}' is available at the configured endpoint.");

            IReadOnlyList<ChatToolCall> toolCalls =
            [
                .. (message.ToolCalls ?? [])
                    .Where(c => c.Id is not null && c.Function?.Name is not null)
                    .Select(c => new ChatToolCall(c.Id!, c.Function!.Name!, c.Function.Arguments ?? "{}"))
            ];

            logger.LogDebug("Completion received. Tool calls requested: {ToolCallCount}", toolCalls.Count);

            return new ChatCompletion(
                message.Content ?? string.Empty,
                toolCalls,
                payload?.Model ?? ModelId);
        }
    }

    private static ChatMessageBody ToWire(ChatMessage message) => new()
    {
        Role = message.Role switch
        {
            ChatRole.System => "system",
            ChatRole.User => "user",
            ChatRole.Assistant => "assistant",
            ChatRole.Tool => "tool",
            _ => throw new ArgumentOutOfRangeException(nameof(message), message.Role, "Unhandled chat role.")
        },
        Content = message.Content,
        ToolCallId = message.ToolCallId
    };

    private static ToolBody ToWire(ChatToolDefinition tool) => new()
    {
        Function = new ToolFunctionBody
        {
            Name = tool.Name,
            Description = tool.Description,
            Parameters = JsonDocument.Parse(tool.ParametersJsonSchema).RootElement.Clone()
        }
    };

    internal static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // The endpoint's own error text is the most actionable thing available — an unknown model
        // name, for instance, is reported here and nowhere else.
        string detail = await response.Content.ReadAsStringAsync(cancellationToken);

        throw new ModelGatewayException(
            $"The model endpoint returned {(int)response.StatusCode} {response.ReasonPhrase}. {detail}".TrimEnd());
    }
}
