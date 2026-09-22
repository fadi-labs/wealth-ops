using Microsoft.Extensions.Configuration;
using WealthOps.Application.Common.Configuration;
using WealthOps.Domain.Enums;

namespace WealthOps.Application.UnitTest.Common.Configuration;

/// <summary>
/// Confirms the configuration keys documented in ADR-003 actually bind to the options graph.
/// </summary>
/// <remarks>
/// Worth testing separately from validation because a key that silently fails to bind leaves a
/// default in place, and a default that happens to be valid would pass every validator while
/// ignoring what the operator configured.
/// </remarks>
public sealed class WealthOpsOptionsBindingTests
{
    [Fact]
    public void TheDocumentedKeysBindToTheOptionsGraph()
    {
        WealthOpsOptions options = Bind(new Dictionary<string, string?>
        {
            ["WealthOps:PersonalDataDirectory"] = "/synthetic/personal",
            ["WealthOps:RulesCacheDirectory"] = "/synthetic/rules-cache",
            ["WealthOps:Instruments"] = "/synthetic/personal/reference/instruments.csv",
            ["WealthOps:Taxation:TaxYears:0"] = "2025",
            ["WealthOps:Taxation:TaxYears:1"] = "2026",
            ["WealthOps:Taxation:Household:Taxpayers:0:Id"] = "taxpayer-a",
            ["WealthOps:Taxation:Household:Taxpayers:0:Municipality"] = "SYNTHETIC_MUNICIPALITY",
            ["WealthOps:Taxation:Household:Taxpayers:0:ChurchTaxMember"] = "true",
            ["WealthOps:Taxation:Household:Taxpayers:0:MaritalStatus"] = "Married",
            ["WealthOps:Taxation:Household:Taxpayers:0:SpouseId"] = "taxpayer-b",
            ["WealthOps:Accounts:0:Id"] = "account-1",
            ["WealthOps:Accounts:0:Wrapper"] = "Aktiesparekonto",
            ["WealthOps:Accounts:0:ExportFormat"] = "FormatB",
            ["WealthOps:Accounts:0:OwnerTaxpayerId"] = "taxpayer-a"
        });

        options.PersonalDataDirectory.ShouldBe("/synthetic/personal");
        options.RulesCacheDirectory.ShouldBe("/synthetic/rules-cache");
        options.Taxation.TaxYears.ShouldBe([2025, 2026]);

        TaxpayerOptions taxpayer = options.Taxation.Household.Taxpayers.ShouldHaveSingleItem();
        taxpayer.Id.ShouldBe("taxpayer-a");
        taxpayer.ChurchTaxMember.ShouldBeTrue();
        taxpayer.MaritalStatus.ShouldBe(MaritalStatus.Married);
        taxpayer.SpouseId.ShouldBe("taxpayer-b");

        AccountOptions account = options.Accounts.ShouldHaveSingleItem();
        account.Wrapper.ShouldBe(AccountWrapper.Aktiesparekonto);
        account.ExportFormat.ShouldBe(TransactionExportFormat.FormatB);
        account.OwnerTaxpayerId.ShouldBe("taxpayer-a");
    }

    [Fact]
    public void EnumsBindByName()
    {
        // The wrapper decides how an account is taxed and the format selects the parser, so a
        // misbound enum is a wrong answer rather than a crash.
        WealthOpsOptions options = Bind(new Dictionary<string, string?>
        {
            ["WealthOps:Accounts:0:Wrapper"] = "FrieMidler",
            ["WealthOps:Accounts:0:ExportFormat"] = "FormatA"
        });

        options.Accounts[0].Wrapper.ShouldBe(AccountWrapper.FrieMidler);
        options.Accounts[0].ExportFormat.ShouldBe(TransactionExportFormat.FormatA);
    }

    [Fact]
    public void AnUnsetChurchTaxMembershipDefaultsToFalse()
    {
        WealthOpsOptions options = Bind(new Dictionary<string, string?>
        {
            ["WealthOps:Taxation:Household:Taxpayers:0:Id"] = "taxpayer-a"
        });

        options.Taxation.Household.Taxpayers[0].ChurchTaxMember.ShouldBeFalse();
    }

    private static WealthOpsOptions Bind(Dictionary<string, string?> values)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new WealthOpsOptions();
        configuration.GetSection(WealthOpsOptions.SectionName).Bind(options);
        return options;
    }
}
