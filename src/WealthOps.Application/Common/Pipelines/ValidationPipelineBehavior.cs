using FluentValidation;
using FluentValidation.Results;
using Mediator;
using WealthOps.Application.Common.Exceptions;

namespace WealthOps.Application.Common.Pipelines;

/// <summary>
/// Runs every registered validator for a request before its handler, and fails fast.
/// </summary>
/// <remarks>
/// Registered through <c>MediatorOptions.PipelineBehaviors</c> rather than as an open generic, so
/// the source generator emits explicit DI registrations — an open-generic registration works under
/// the runtime container but not under NativeAOT, and the generator is the single source of truth
/// for handler wiring.
/// </remarks>
public sealed class ValidationPipelineBehavior<TMessage, TResponse>(
    IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        IValidator<TMessage>[] applicable = [.. validators];

        if (applicable.Length == 0)
        {
            return await next(message, cancellationToken);
        }

        var context = new ValidationContext<TMessage>(message);

        ValidationResult[] results = await Task.WhenAll(
            applicable.Select(v => v.ValidateAsync(context, cancellationToken)));

        ValidationFailure[] failures = [.. results.SelectMany(r => r.Errors).Where(f => f is not null)];

        if (failures.Length > 0)
        {
            throw new RequestValidationException(typeof(TMessage).Name, failures);
        }

        return await next(message, cancellationToken);
    }
}
