using System.Text.Json.Serialization;

namespace WealthOps.Infrastructure.Clients.OpenAiCompatible;

/// <summary>
/// Wire shapes for the OpenAI-compatible protocol.
/// </summary>
/// <remarks>
/// Internal and deliberately anaemic. These types exist only to cross the HTTP boundary; the
/// Application-facing contracts in <c>Common/Clients</c> are the ones the rest of the system sees,
/// so a protocol change lands here and nowhere else (EP-3).
/// </remarks>
internal sealed record ChatCompletionRequestBody
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("messages")]
    public required IReadOnlyList<ChatMessageBody> Messages { get; init; }

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ToolBody>? Tools { get; init; }

    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; init; }

    [JsonPropertyName("stream")]
    public bool Stream => false;
}

internal sealed record ChatMessageBody
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; init; }
}

internal sealed record ToolBody
{
    [JsonPropertyName("type")]
    public string Type => "function";

    [JsonPropertyName("function")]
    public required ToolFunctionBody Function { get; init; }
}

internal sealed record ToolFunctionBody
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("description")]
    public required string Description { get; init; }

    [JsonPropertyName("parameters")]
    public required System.Text.Json.JsonElement Parameters { get; init; }
}

internal sealed record ChatCompletionResponseBody
{
    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("choices")]
    public IReadOnlyList<ChoiceBody>? Choices { get; init; }
}

internal sealed record ChoiceBody
{
    [JsonPropertyName("message")]
    public ResponseMessageBody? Message { get; init; }
}

internal sealed record ResponseMessageBody
{
    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("tool_calls")]
    public IReadOnlyList<ToolCallBody>? ToolCalls { get; init; }
}

internal sealed record ToolCallBody
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("function")]
    public ToolCallFunctionBody? Function { get; init; }
}

internal sealed record ToolCallFunctionBody
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; init; }
}

internal sealed record EmbeddingRequestBody
{
    [JsonPropertyName("model")]
    public required string Model { get; init; }

    [JsonPropertyName("input")]
    public required IReadOnlyList<string> Input { get; init; }
}

internal sealed record EmbeddingResponseBody
{
    [JsonPropertyName("data")]
    public IReadOnlyList<EmbeddingDataBody>? Data { get; init; }
}

internal sealed record EmbeddingDataBody
{
    [JsonPropertyName("index")]
    public int Index { get; init; }

    [JsonPropertyName("embedding")]
    public float[]? Embedding { get; init; }
}

internal sealed record ModelListResponseBody
{
    [JsonPropertyName("data")]
    public IReadOnlyList<ModelEntryBody>? Data { get; init; }
}

internal sealed record ModelEntryBody
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }
}
