using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WealthOps.Application.Common.Configuration;
using WealthOps.Application.Common.Pipelines;

namespace WealthOps.Application.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Application layer: Mediator, validators, and the operator configuration.
    /// </summary>
    /// <remarks>
    /// Called identically by both composition roots (ADR-001), which is what keeps the CLI and the
    /// Host from drifting apart as keys are added.
    /// </remarks>
    public static IServiceCollection AddApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddMediator(options =>
        {
            options.Namespace = "WealthOps.Application";

            // The generator defaults to Singleton. Handlers consume DbContext, so Singleton fails
            // DI scope validation at startup — see APPLICATION_AGENTS.md.
            options.ServiceLifetime = ServiceLifetime.Scoped;

            options.PipelineBehaviors = [typeof(ValidationPipelineBehavior<,>)];
        });

        services.AddValidatorsFromAssemblyContaining<WealthOpsOptionsValidator>(
            lifetime: ServiceLifetime.Scoped,
            includeInternalTypes: true);

        services.AddWealthOpsOptions(configuration);
        services.TryAddTimeProvider();

        return services;
    }

    /// <summary>
    /// Binds and validates <see cref="WealthOpsOptions"/>, failing at startup rather than at use.
    /// </summary>
    /// <remarks>
    /// <c>ValidateOnStart</c> is the point of this method. A configuration error that surfaces
    /// halfway through an ingestion run is far more expensive than one that prevents boot
    /// (ADR-003).
    /// </remarks>
    public static IServiceCollection AddWealthOpsOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<WealthOpsOptions>, WealthOpsOptionsStartupValidator>();

        services.AddOptions<WealthOpsOptions>()
            .Bind(configuration.GetSection(WealthOpsOptions.SectionName))
            .ValidateOnStart();

        return services;
    }

    private static IServiceCollection TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.All(d => d.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }

        return services;
    }
}

/// <summary>
/// Reports configuration failures with every message, not just the first.
/// </summary>
/// <remarks>
/// An operator setting this up for the first time should see everything that is wrong in one pass,
/// rather than fixing one key, rebooting, and discovering the next.
/// </remarks>
internal sealed class WealthOpsOptionsStartupValidator : IValidateOptions<WealthOpsOptions>
{
    private readonly WealthOpsOptionsValidator _validator = new();

    public ValidateOptionsResult Validate(string? name, WealthOpsOptions options)
    {
        FluentValidation.Results.ValidationResult result = _validator.Validate(options);

        return result.IsValid
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(result.Errors.Select(e => e.ErrorMessage));
    }
}
