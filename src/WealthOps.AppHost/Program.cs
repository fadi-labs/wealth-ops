using System.Diagnostics.CodeAnalysis;

[assembly: ExcludeFromCodeCoverage]

var builder = DistributedApplication.CreateBuilder(args);

// pgvector rather than stock postgres: the vector extension is declared on the EF model, so the
// initial migration issues CREATE EXTENSION and a plain image fails there rather than later.
var postgres = builder.AddPostgres("postgres")
    .WithImage("pgvector/pgvector")
    .WithImageTag("pg17")
    // Ingested data must outlive a container restart — re-embedding a corpus is slow on local
    // CPU inference, and an operator who loses it twice stops trusting the tool.
    .WithDataVolume("wealthops-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("wealthops");

// Local inference is an EXTERNAL HOST PROCESS, not an Aspire resource: it is installed on the
// machine, may be GPU-bound, and is deliberately not part of the stack's lifecycle. The AppHost
// therefore passes only its address (EP-3). The stack starts fine without it — status reports the
// endpoint as unreachable, which is also what lets the test suite run with no model at all (NFR-3).
var modelEndpoint = builder.Configuration["WealthOps:Models:Endpoint"]
    ?? "http://host.docker.internal:11434/v1";

builder.AddProject<Projects.WealthOps_Host>("host")
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment("WealthOps__Models__Endpoint", modelEndpoint);

// Explicit start: this is an interactive console application. Auto-starting it under the
// dashboard would spawn a chat REPL with nowhere to type, and it would exit immediately on the
// closed stdin — which reads in the dashboard as a crash loop rather than as "not applicable".
builder.AddProject<Projects.WealthOps_Cli>("cli")
    .WithReference(database)
    .WaitFor(database)
    .WithEnvironment("WealthOps__Models__Endpoint", modelEndpoint)
    .WithArgs("status")
    .WithExplicitStart();

builder.Build().Run();
