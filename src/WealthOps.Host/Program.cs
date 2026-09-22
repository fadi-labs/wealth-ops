using WealthOps.Application.Extensions;
using WealthOps.Host.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWealthOps(builder.Configuration);

var app = builder.Build();

app.MapWealthOpsHealthChecks();

app.Run();

// Exposed so integration tests can target the entry point via WebApplicationFactory<Program>.
//
// v1 maps a health probe and nothing else: the CLI is the operator surface and composes the
// application in-process (ADR-001), so there is no consumer for an HTTP API yet. GET / is
// deliberately left unmapped — the integration smoke test asserts a 404 there as proof the
// pipeline booted.
public partial class Program { }
