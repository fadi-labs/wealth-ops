using FluentValidation;
using Microsoft.Extensions.Options;

namespace WealthOps.Infrastructure.Clients.OpenAiCompatible;

/// <summary>
/// Where the model gateway points and which models it should ask for.
/// </summary>
/// <remarks>
/// <para>
/// This is EP-3 in configuration form. Every value here is a knob, which is what makes AC-12
/// ("switching chat or embedding model is a configuration change only") hold: no model identifier
/// appears anywhere in code (BR-13, AC-12).
/// </para>
/// <para>
/// In v1 <see cref="Endpoint"/> addresses a process on the operator's own machine. Nothing checks
/// that, because it is a deployment fact rather than a code fact — but BR-1 means pointing it at a
/// remote provider would break the system's central promise, not merely change its performance.
/// </para>
/// </remarks>
public sealed class ModelGatewayOptions
{
    public const string SectionName = "WealthOps:Models";

    /// <summary>Base address of the OpenAI-compatible endpoint, e.g. <c>http://127.0.0.1:11434/v1</c>.</summary>
    public string Endpoint { get; set; } = string.Empty;

    public ChatModelOptions Chat { get; set; } = new();

    public EmbeddingModelOptions Embedding { get; set; } = new();

    /// <summary>
    /// How long to wait for a completion.
    /// </summary>
    /// <remarks>
    /// Generous by HTTP standards because local inference on CPU is slow, and a timeout here reads
    /// to the operator as "the model is broken" rather than "the model is thinking".
    /// </remarks>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}

public sealed class ChatModelOptions
{
    /// <summary>The chat model identifier the endpoint knows it by.</summary>
    public string Id { get; set; } = string.Empty;
}

public sealed class EmbeddingModelOptions
{
    /// <summary>The embedding model identifier the endpoint knows it by.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The dimension this model produces.
    /// </summary>
    /// <remarks>
    /// Declared here rather than discovered at runtime so that a mismatch between configuration
    /// and reality is caught on the first embedding call, before anything is stored (ADR-002).
    /// </remarks>
    public int Dimensions { get; set; }
}

internal sealed class ModelGatewayOptionsValidator : AbstractValidator<ModelGatewayOptions>
{
    public ModelGatewayOptionsValidator()
    {
        RuleFor(o => o.Endpoint)
            .NotEmpty()
            .WithMessage("WealthOps:Models:Endpoint must be set.")
            .Must(BeAnAbsoluteHttpUri)
            .WithMessage("WealthOps:Models:Endpoint must be an absolute http or https URI.");

        RuleFor(o => o.Chat.Id)
            .NotEmpty()
            .WithMessage("WealthOps:Models:Chat:Id must name the chat model.");

        RuleFor(o => o.Embedding.Id)
            .NotEmpty()
            .WithMessage("WealthOps:Models:Embedding:Id must name the embedding model.");

        RuleFor(o => o.Embedding.Dimensions)
            .GreaterThan(0)
            .WithMessage("WealthOps:Models:Embedding:Dimensions must be the positive width the model produces.");

        RuleFor(o => o.Timeout)
            .GreaterThan(TimeSpan.Zero)
            .WithMessage("WealthOps:Models:Timeout must be positive.");
    }

    private static bool BeAnAbsoluteHttpUri(string endpoint)
        => Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

internal sealed class ModelGatewayOptionsStartupValidator : IValidateOptions<ModelGatewayOptions>
{
    private readonly ModelGatewayOptionsValidator _validator = new();

    public ValidateOptionsResult Validate(string? name, ModelGatewayOptions options)
    {
        FluentValidation.Results.ValidationResult result = _validator.Validate(options);

        return result.IsValid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(result.Errors.Select(e => e.ErrorMessage));
    }
}
