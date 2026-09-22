namespace WealthOps.Application.Common.Clients;

/// <summary>Who produced a turn in a conversation.</summary>
public enum ChatRole
{
    System = 1,
    User = 2,
    Assistant = 3,

    /// <summary>The result of a tool invocation, fed back to the model.</summary>
    Tool = 4
}

/// <summary>
/// One turn in a conversation.
/// </summary>
/// <param name="Role">Who produced this turn.</param>
/// <param name="Content">The turn's text.</param>
/// <param name="ToolCallId">
/// For a <see cref="ChatRole.Tool"/> turn, the identifier of the call this result answers.
/// </param>
public sealed record ChatMessage(ChatRole Role, string Content, string? ToolCallId = null)
{
    public static ChatMessage System(string content) => new(ChatRole.System, content);

    public static ChatMessage User(string content) => new(ChatRole.User, content);

    public static ChatMessage Assistant(string content) => new(ChatRole.Assistant, content);

    public static ChatMessage ToolResult(string toolCallId, string content)
        => new(ChatRole.Tool, content, toolCallId);
}

/// <summary>
/// A tool the model may call, described by a JSON Schema parameter object.
/// </summary>
/// <remarks>
/// Present from M0 although nothing calls a tool until M3. The reason is BR-2: the model must
/// never produce a monetary figure from its own reasoning, so every DKK amount has to arrive
/// through a typed tool contract (FR-M3-9). Defining the seam now keeps that from becoming a
/// retrofit of <see cref="IChatModel"/> once calculators exist.
/// </remarks>
/// <param name="Name">Tool identifier, unique within a request.</param>
/// <param name="Description">What the tool does, in terms the model can select on.</param>
/// <param name="ParametersJsonSchema">JSON Schema for the tool's parameter object.</param>
public sealed record ChatToolDefinition(string Name, string Description, string ParametersJsonSchema);

/// <summary>A tool invocation requested by the model.</summary>
/// <param name="Id">Correlates this call with the <see cref="ChatMessage.ToolResult"/> that answers it.</param>
/// <param name="Name">The <see cref="ChatToolDefinition.Name"/> being invoked.</param>
/// <param name="ArgumentsJson">Arguments as a JSON object, unvalidated.</param>
public sealed record ChatToolCall(string Id, string Name, string ArgumentsJson);

/// <summary>A completion request: the conversation so far, plus any tools on offer.</summary>
/// <param name="Messages">The conversation, oldest first.</param>
/// <param name="Tools">Tools the model may call. Empty in M0.</param>
/// <param name="Temperature">
/// Sampling temperature, where the endpoint supports it. Left unset to use the endpoint default.
/// </param>
public sealed record ChatCompletionRequest(
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<ChatToolDefinition>? Tools = null,
    double? Temperature = null);

/// <summary>
/// A completion.
/// </summary>
/// <param name="Content">
/// The assistant's text. Empty when the model chose to call tools instead of answering.
/// </param>
/// <param name="ToolCalls">Tool invocations the model requested. Empty when it answered directly.</param>
/// <param name="ModelId">The model that produced this completion, as reported by the endpoint.</param>
public sealed record ChatCompletion(
    string Content,
    IReadOnlyList<ChatToolCall> ToolCalls,
    string ModelId);
