using Microsoft.Extensions.Options;
using WealthOps.Infrastructure.Clients.OpenAiCompatible;

namespace WealthOps.Infrastructure.UnitTest.Clients.OpenAiCompatible;

public sealed class ModelGatewayOptionsValidatorTests
{
    private static readonly ModelGatewayOptionsStartupValidator _validator = new();

    [Fact]
    public void AFullyConfiguredGatewayPasses()
        => _validator.Validate(null, GatewayTestOptions.Create()).Succeeded.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("not-a-uri")]
    [InlineData("/v1")]
    [InlineData("ftp://127.0.0.1/v1")]
    public void TheEndpointMustBeAnAbsoluteHttpUri(string endpoint)
    {
        ModelGatewayOptions options = GatewayTestOptions.Create();
        options.Endpoint = endpoint;

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void TheChatModelMustBeNamed()
    {
        ModelGatewayOptions options = GatewayTestOptions.Create();
        options.Chat.Id = string.Empty;

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void TheEmbeddingModelMustBeNamed()
    {
        ModelGatewayOptions options = GatewayTestOptions.Create();
        options.Embedding.Id = string.Empty;

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void TheEmbeddingWidthMustBePositive(int dimensions)
    {
        // Zero would make every stored vector "match" a zero-width configuration and defeat the
        // mismatch check that ADR-002 rests on.
        ModelGatewayOptions options = GatewayTestOptions.Create(dimensions);

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void TheTimeoutMustBePositive()
    {
        ModelGatewayOptions options = GatewayTestOptions.Create();
        options.Timeout = TimeSpan.Zero;

        _validator.Validate(null, options).Failed.ShouldBeTrue();
    }

    [Fact]
    public void EveryFailureIsReportedTogether()
    {
        // An operator configuring this for the first time should see everything wrong at once,
        // not fix one key and reboot to find the next.
        var options = new ModelGatewayOptions();

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
        result.Failures!.Count().ShouldBeGreaterThan(1);
    }
}
