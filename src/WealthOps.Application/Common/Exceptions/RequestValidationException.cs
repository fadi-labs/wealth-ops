using FluentValidation.Results;

namespace WealthOps.Application.Common.Exceptions;

/// <summary>
/// A request failed validation in the Mediator pipeline, before its handler ran.
/// </summary>
public sealed class RequestValidationException : Exception
{
    public RequestValidationException(string requestName, IReadOnlyList<ValidationFailure> failures)
        : base($"{requestName} is not valid: " +
               string.Join("; ", failures.Select(f => $"{f.PropertyName}: {f.ErrorMessage}")))
    {
        RequestName = requestName;
        Failures = failures;
    }

    public string RequestName { get; }

    public IReadOnlyList<ValidationFailure> Failures { get; }
}
