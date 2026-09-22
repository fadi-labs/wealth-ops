using WealthOps.Application.Extensions;
using WealthOps.Infrastructure.Extensions;

namespace WealthOps.Host.Configuration;

internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Composes the application layers.
    /// </summary>
    /// <remarks>
    /// The same pair of calls the CLI makes. Keeping both roots on one registration path is what
    /// stops a configuration key added for one from being missing in the other (ADR-001).
    /// </remarks>
    public static IServiceCollection AddWealthOps(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddApplication(configuration);
        services.AddInfrastructure(configuration);

        return services;
    }
}
