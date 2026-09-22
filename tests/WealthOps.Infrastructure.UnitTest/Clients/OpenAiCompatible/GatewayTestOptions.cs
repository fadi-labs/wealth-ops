using WealthOps.Infrastructure.Clients.OpenAiCompatible;

namespace WealthOps.Infrastructure.UnitTest.Clients.OpenAiCompatible;

/// <summary>
/// Invented gateway configuration. Model identifiers here are placeholders, never real ones —
/// no model name belongs in code or a test (BR-13, AC-12).
/// </summary>
internal static class GatewayTestOptions
{
    public const string ChatModelId = "synthetic-chat-model";
    public const string EmbeddingModelId = "synthetic-embedding-model";
    public const int Dimensions = 4;

    public static ModelGatewayOptions Create(int dimensions = Dimensions) => new()
    {
        Endpoint = "http://127.0.0.1:1/v1",
        Chat = new ChatModelOptions { Id = ChatModelId },
        Embedding = new EmbeddingModelOptions { Id = EmbeddingModelId, Dimensions = dimensions },
        Timeout = TimeSpan.FromSeconds(30)
    };
}
