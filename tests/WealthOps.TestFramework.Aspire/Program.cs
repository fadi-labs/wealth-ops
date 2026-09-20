using System.Diagnostics.CodeAnalysis;
using WealthOps.TestFramework.Aspire;

[assembly: ExcludeFromCodeCoverage]

var builder = DistributedApplication.CreateBuilder(args);
builder.AddWealthOpsTestDependencies();

builder.Build().Run();
