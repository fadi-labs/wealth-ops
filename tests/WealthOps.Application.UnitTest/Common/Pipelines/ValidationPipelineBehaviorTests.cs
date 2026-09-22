using FluentValidation;
using Mediator;
using WealthOps.Application.Common.Exceptions;
using WealthOps.Application.Common.Pipelines;

namespace WealthOps.Application.UnitTest.Common.Pipelines;

public sealed class ValidationPipelineBehaviorTests
{
    [Fact]
    public async Task AValidMessageReachesItsHandler()
    {
        var behavior = new ValidationPipelineBehavior<SampleRequest, string>([new SampleValidator()]);
        bool handlerRan = false;

        string result = await behavior.Handle(
            new SampleRequest("something"),
            (_, _) =>
            {
                handlerRan = true;
                return ValueTask.FromResult("handled");
            },
            TestContext.Current.CancellationToken);

        handlerRan.ShouldBeTrue();
        result.ShouldBe("handled");
    }

    [Fact]
    public async Task AnInvalidMessageNeverReachesItsHandler()
    {
        // Fail-fast is the point: a handler that has already opened a transaction or started
        // walking a directory is far more expensive to unwind than a rejected request.
        var behavior = new ValidationPipelineBehavior<SampleRequest, string>([new SampleValidator()]);
        bool handlerRan = false;

        await Should.ThrowAsync<RequestValidationException>(async () => await behavior.Handle(
            new SampleRequest(""),
            (_, _) =>
            {
                handlerRan = true;
                return ValueTask.FromResult("handled");
            },
            TestContext.Current.CancellationToken));

        handlerRan.ShouldBeFalse();
    }

    [Fact]
    public async Task TheExceptionNamesTheRequestAndEveryFailure()
    {
        var behavior = new ValidationPipelineBehavior<SampleRequest, string>([new SampleValidator()]);

        RequestValidationException exception = await Should.ThrowAsync<RequestValidationException>(
            async () => await behavior.Handle(
                new SampleRequest(""),
                (_, _) => ValueTask.FromResult("handled"),
                TestContext.Current.CancellationToken));

        exception.RequestName.ShouldBe(nameof(SampleRequest));
        exception.Failures.ShouldNotBeEmpty();
        exception.Message.ShouldContain(nameof(SampleRequest.Value));
    }

    [Fact]
    public async Task AMessageWithNoValidatorPassesThrough()
    {
        var behavior = new ValidationPipelineBehavior<SampleRequest, string>([]);

        string result = await behavior.Handle(
            new SampleRequest(""),
            (_, _) => ValueTask.FromResult("handled"),
            TestContext.Current.CancellationToken);

        result.ShouldBe("handled");
    }

    public sealed record SampleRequest(string Value) : IRequest<string>;

    private sealed class SampleValidator : AbstractValidator<SampleRequest>
    {
        public SampleValidator() => RuleFor(r => r.Value).NotEmpty();
    }
}
