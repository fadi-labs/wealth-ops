using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace WealthOps.TestFramework.Aspire;

internal static class DistributedApplicationBuilderExtensions
{
    private const string DockerDesktopGroupName = "project";

    internal static void AddWealthOpsTestDependencies(this IDistributedApplicationBuilder builder)
    {
        var postgresPassword = builder.AddParameter(
            "postgres-password",
            "LocalMachineAccessNoInterestingDataTestDev#Passw0rd!FirewallNotExposed",
            secret: true);

        builder.AddPostgresDependency(postgresPassword);
        builder.AddRedisDependency();
        builder.AddWireMockDependency();
    }

    private static void AddPostgresDependency(
        this IDistributedApplicationBuilder builder,
        IResourceBuilder<ParameterResource> postgresPassword)
    {
        // pgvector, not stock postgres: the application's initial migration issues
        // CREATE EXTENSION vector, and L1 component tests round-trip a real vector column.
        //
        // Name and port are unchanged so CI (.github/actions/aspire-test-with-coverage/run.sh)
        // and .docs/wiki/testing.md stay valid. A developer who pre-warmed a stock-postgres
        // container before this change must remove it once —
        //   docker rm -f project-test-postgres
        // — otherwise the fixed-endpoint probe reuses it and migrations fail on the missing
        // extension. WealthOpsTestDatabase reports exactly that if it happens.
        var postgres = builder.AddPostgres("postgres", password: postgresPassword, port: 15432)
            .WithImage("pgvector/pgvector")
            .WithImageTag("pg17")
            .WithContainerName("project-test-postgres")
            .WithContainerRuntimeArgs(
                "--label", $"com.docker.compose.project={DockerDesktopGroupName}",
                "--label", "com.docker.compose.service=project-test-postgres")
            .WithLifetime(ContainerLifetime.Persistent);

        postgres.AddDatabase("app-component");
        postgres.AddDatabase("app-integration");
        postgres.AddDatabase("infra-component");
        postgres.AddDatabase("infra-integration");
        postgres.AddDatabase("host-integration");
    }

    private static void AddRedisDependency(this IDistributedApplicationBuilder builder)
    {
        builder.AddRedis("redis", port: 16379)
            .WithContainerName("project-test-redis")
            .WithContainerRuntimeArgs(
                "--label", $"com.docker.compose.project={DockerDesktopGroupName}",
                "--label", "com.docker.compose.service=project-test-redis")
            .WithLifetime(ContainerLifetime.Persistent);
    }

    private static void AddWireMockDependency(this IDistributedApplicationBuilder builder)
    {
        builder.AddContainer("wiremock", "wiremock/wiremock")
            .WithHttpEndpoint(port: 19091, targetPort: 8080)
            .WithContainerName("project-test-wiremock")
            .WithContainerRuntimeArgs(
                "--label", $"com.docker.compose.project={DockerDesktopGroupName}",
                "--label", "com.docker.compose.service=project-test-wiremock")
            .WithLifetime(ContainerLifetime.Persistent);
    }
}
