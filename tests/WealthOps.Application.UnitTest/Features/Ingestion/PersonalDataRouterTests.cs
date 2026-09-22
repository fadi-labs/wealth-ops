using WealthOps.Application.Features.Ingestion;
using WealthOps.Domain.Enums;

namespace WealthOps.Application.UnitTest.Features.Ingestion;

public sealed class PersonalDataRouterTests
{
    [Theory]
    [InlineData("transactions/format-a/export.csv", DocumentType.TransactionExportFormatA)]
    [InlineData("transactions/format-b/export.csv", DocumentType.TransactionExportFormatB)]
    [InlineData("documents/payslips/2026-01.pdf", DocumentType.Payslip)]
    [InlineData("documents/tax-statements/2025.pdf", DocumentType.TaxStatement)]
    [InlineData("documents/annual-statements/2025.pdf", DocumentType.AnnualStatement)]
    [InlineData("documents/loan/schedule.pdf", DocumentType.LoanSchedule)]
    [InlineData("reference/instruments.csv", DocumentType.Reference)]
    [InlineData("reconciliation/arsopgorelse-2025.json", DocumentType.Reference)]
    public void RoutesTheDocumentedLayout(string relativePath, DocumentType expected)
        => PersonalDataRouter.Route(relativePath).ShouldBe(expected);

    [Fact]
    public void AcceptsWindowsSeparators()
    {
        // The same tree is walked on both platforms; routing must not depend on which one.
        PersonalDataRouter.Route(@"documents\payslips\2026-01.pdf").ShouldBe(DocumentType.Payslip);
    }

    [Fact]
    public void RoutesNestedFilesByTheirTopFolders()
    {
        PersonalDataRouter.Route("documents/payslips/2026/january.pdf").ShouldBe(DocumentType.Payslip);
    }

    [Theory]
    [InlineData("stray.pdf")]
    [InlineData("unknown-folder/file.pdf")]
    [InlineData("documents/unknown-kind/file.pdf")]
    [InlineData("transactions/format-c/export.csv")]
    public void UnrecognisedPositionsBecomeUnclassified(string relativePath)
    {
        // Reported rather than guessed at or dropped (BR-10, NFR-7) — ingest surfaces the count
        // so the operator can move the file or the routing table can grow.
        PersonalDataRouter.Route(relativePath).ShouldBe(DocumentType.Unclassified);
    }

    [Fact]
    public void RoutingIsCaseInsensitive()
        => PersonalDataRouter.Route("Documents/PaySlips/2026-01.pdf").ShouldBe(DocumentType.Payslip);

    [Fact]
    public void RejectsANullPath()
        => Should.Throw<ArgumentNullException>(() => PersonalDataRouter.Route(null!));
}
