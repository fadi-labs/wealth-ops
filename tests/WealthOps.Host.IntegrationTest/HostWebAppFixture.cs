extern alias HostApp;

using WealthOps.Infrastructure.Persistence.Extensions;
using WealthOps.TestFramework.Fixtures;

namespace WealthOps.Host.IntegrationTest;

/// <summary>
/// Closes the generic <see cref="WebAppFixture{TProgram}"/> over the Host's entry point.
/// The <c>HostApp</c> extern alias disambiguates the Host's <c>Program</c> from the test
/// assembly's own auto-generated <c>Program</c> (xunit.v3 compiles test projects as executables).
/// </summary>
/// <remarks>
/// <para>
/// The Host composes Application and Infrastructure, so booting it needs a real database and a
/// complete, valid <c>WealthOps:*</c> configuration — startup validation refuses to run without
/// one (ADR-003). Supplying it here is also a standing check that the two composition roots agree
/// on the key surface: a key added for the CLI and forgotten here fails this fixture.
/// </para>
/// <para>
/// Every value below is invented. The model endpoint points at a closed loopback port on purpose:
/// no test may require a running language model (NFR-3), and the Host's health probe deliberately
/// excludes the model endpoint.
/// </para>
/// </remarks>
public sealed class HostWebAppFixture : WebAppFixture<HostApp::Program>
{
    private readonly string _personalDataDirectory = Path.Combine(
        Path.GetTempPath(),
        $"wealthops-synthetic-{Guid.NewGuid():N}");

    private readonly string _rulesCacheDirectory = Path.Combine(
        Path.GetTempPath(),
        $"wealthops-synthetic-rules-{Guid.NewGuid():N}");

    protected override bool RecreateDatabaseOnInitialize => true;

    protected override Task EnrichConfigurationAsync(Dictionary<string, string?> overrides)
    {
        overrides["ConnectionStrings:wealthops"] =
            Aspire.CreateDatabaseConnectionString(DatabaseName) + ";SSL Mode=Disable";

        overrides["WealthOps:PersonalDataDirectory"] = _personalDataDirectory;
        overrides["WealthOps:RulesCacheDirectory"] = _rulesCacheDirectory;
        overrides["WealthOps:Instruments"] = Path.Combine(_personalDataDirectory, "reference", "instruments.csv");
        overrides["WealthOps:Taxation:TaxYears:0"] = "2026";
        overrides["WealthOps:Taxation:Household:Taxpayers:0:Id"] = "taxpayer-a";
        overrides["WealthOps:Taxation:Household:Taxpayers:0:Municipality"] = "SYNTHETIC_MUNICIPALITY";
        overrides["WealthOps:Taxation:Household:Taxpayers:0:ChurchTaxMember"] = "false";
        overrides["WealthOps:Taxation:Household:Taxpayers:0:MaritalStatus"] = "Single";
        overrides["WealthOps:Accounts:0:Id"] = "account-1";
        overrides["WealthOps:Accounts:0:Wrapper"] = "FrieMidler";
        overrides["WealthOps:Accounts:0:ExportFormat"] = "FormatA";
        overrides["WealthOps:Accounts:0:OwnerTaxpayerId"] = "taxpayer-a";

        // A closed loopback port: reachable configuration, unreachable service.
        overrides["WealthOps:Models:Endpoint"] = "http://127.0.0.1:1/v1";
        overrides["WealthOps:Models:Chat:Id"] = "synthetic-chat-model";
        overrides["WealthOps:Models:Embedding:Id"] = "synthetic-embedding-model";
        overrides["WealthOps:Models:Embedding:Dimensions"] = "4";

        return Task.CompletedTask;
    }

    protected override Task PostInitializeAsync()
        // The Host does not migrate on startup — only the CLI does — so the schema is applied here,
        // through the same extension the CLI uses.
        => Services.MigrateWealthOpsAsync();
}
