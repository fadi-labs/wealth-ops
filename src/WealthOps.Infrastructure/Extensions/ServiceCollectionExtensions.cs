using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using WealthOps.Application.Common.Clients;
using WealthOps.Application.Common.Persistence;
using WealthOps.Infrastructure.Clients.OpenAiCompatible;
using WealthOps.Infrastructure.Persistence;
using WealthOps.Infrastructure.Persistence.Diagnostics;
using WealthOps.Infrastructure.Persistence.Stores;

namespace WealthOps.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>The connection string name both composition roots resolve the database by.</summary>
    public const string DatabaseConnectionName = "wealthops";

    /// <summary>
    /// Registers persistence and the model gateway.
    /// </summary>
    /// <remarks>
    /// Called identically by the CLI and the Host (ADR-001).
    /// </remarks>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddWealthOpsPersistence(configuration);
        services.AddModelGateway(configuration);

        return services;
    }

    private static IServiceCollection AddWealthOpsPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // A shared data source rather than a connection string per context: registering the
        // pgvector type handler is a data-source-level concern, and doing it once here is what
        // makes Vector round-trip at all.
        //
        // The connection string is resolved from the container, not captured here. Reading it at
        // registration time would bind whatever configuration existed when AddInfrastructure was
        // called — which under WebApplicationFactory is before the test host has applied its
        // overrides, and in general is a trap for any late-arriving configuration source.
        services.AddSingleton(provider =>
        {
            IConfiguration resolved = provider.GetService<IConfiguration>() ?? configuration;

            string connectionString =
                resolved.GetConnectionString(DatabaseConnectionName)
                ?? throw new InvalidOperationException(
                    $"No connection string named '{DatabaseConnectionName}' was configured. " +
                    "Run under the Aspire AppHost, or set ConnectionStrings:wealthops.");

            var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
            dataSourceBuilder.UseVector();
            return dataSourceBuilder.Build();
        });

        services.AddDbContext<WealthOpsDbContext>((provider, options) =>
        {
            var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
            options.UseNpgsql(dataSource, npgsql =>
            {
                npgsql.UseVector();
                npgsql.MigrationsHistoryTable("__EFMigrationsHistory");
            });
        });

        services.AddScoped<IDocumentStore, DocumentStore>();
        services.AddScoped<IVectorStore, VectorStore>();
        services.AddScoped<IPersistenceDiagnostics, PersistenceDiagnostics>();

        return services;
    }

    private static IServiceCollection AddModelGateway(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<ModelGatewayOptions>, ModelGatewayOptionsStartupValidator>();

        services.AddOptions<ModelGatewayOptions>()
            .Bind(configuration.GetSection(ModelGatewayOptions.SectionName))
            .ValidateOnStart();

        services.AddHttpClient<IChatModel, ChatCompletionsClient>(ConfigureGatewayClient);
        services.AddHttpClient<IEmbeddingModel, EmbeddingsClient>(ConfigureGatewayClient);
        services.AddHttpClient<IModelEndpointProbe, ModelEndpointProbe>(ConfigureGatewayClient);

        return services;
    }

    private static void ConfigureGatewayClient(IServiceProvider provider, HttpClient client)
    {
        ModelGatewayOptions options = provider.GetRequiredService<IOptions<ModelGatewayOptions>>().Value;

        // A trailing slash makes relative request URIs ("chat/completions") resolve under the
        // configured path rather than replacing its last segment — without it, an endpoint
        // configured as ".../v1" silently loses the "/v1".
        string endpoint = options.Endpoint.EndsWith('/') ? options.Endpoint : options.Endpoint + "/";

        client.BaseAddress = new Uri(endpoint, UriKind.Absolute);
        client.Timeout = options.Timeout;
    }
}
