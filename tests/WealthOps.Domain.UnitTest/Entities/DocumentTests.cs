using WealthOps.Domain.Entities;
using WealthOps.Domain.Enums;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Domain.UnitTest.Entities;

public sealed class DocumentTests
{
    private static readonly DateTimeOffset _ingestedAt = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    // Synthetic throughout — an invented path under an invented root.
    private static readonly string _syntheticPath =
        Path.Combine(Path.GetTempPath(), "wealthops-synthetic", "documents", "payslips", "sample.pdf");

    [Fact]
    public void Create_AssignsAnIdentity()
    {
        Document document = CreateDocument();

        document.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Create_RejectsARelativePath()
    {
        // An absolute path keeps the source file resolvable regardless of working directory —
        // which matters because ingest may be run from anywhere.
        Should.Throw<ArgumentException>(() => Document.Create(
            Path.Combine("documents", "payslips", "sample.pdf"),
            DocumentType.Payslip,
            ContentHash.FromBytes("synthetic"u8),
            Corpus.Personal,
            _ingestedAt));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsABlankPath(string path)
        => Should.Throw<ArgumentException>(() => Document.Create(
            path,
            DocumentType.Payslip,
            ContentHash.FromBytes("synthetic"u8),
            Corpus.Personal,
            _ingestedAt));

    [Fact]
    public void Create_RejectsABlankTaxpayerIdentifier()
    {
        // Absent is meaningful; blank is a mistake that would compare unequal to absent.
        Should.Throw<ArgumentException>(() => Document.Create(
            _syntheticPath,
            DocumentType.Payslip,
            ContentHash.FromBytes("synthetic"u8),
            Corpus.Personal,
            _ingestedAt,
            taxpayerId: "  "));
    }

    [Fact]
    public void Create_AcceptsAnAbsentTaxpayerAndTaxYear()
    {
        Document document = CreateDocument();

        document.TaxpayerId.ShouldBeNull();
        document.TaxYear.ShouldBeNull();
    }

    [Fact]
    public void Create_CarriesTheSuppliedTaxYear()
    {
        Document document = Document.Create(
            _syntheticPath,
            DocumentType.TaxStatement,
            ContentHash.FromBytes("synthetic"u8),
            Corpus.Personal,
            _ingestedAt,
            taxpayerId: "taxpayer-a",
            taxYear: TaxYear.Create(2026));

        document.TaxYear!.Value.Value.ShouldBe(2026);
        document.TaxpayerId.ShouldBe("taxpayer-a");
    }

    [Fact]
    public void RelocateTo_UpdatesThePathWithoutChangingIdentity()
    {
        Document document = CreateDocument();
        Guid originalId = document.Id;
        ContentHash originalHash = document.ContentHash;

        string moved = Path.Combine(Path.GetTempPath(), "wealthops-synthetic", "archive", "sample.pdf");
        document.RelocateTo(moved);

        // Content identity outranks location: the same file moved is the same document.
        document.AbsolutePath.ShouldBe(moved);
        document.Id.ShouldBe(originalId);
        document.ContentHash.ShouldBe(originalHash);
    }

    [Fact]
    public void RelocateTo_RejectsARelativePath()
    {
        Document document = CreateDocument();

        Should.Throw<ArgumentException>(() => document.RelocateTo("archive/sample.pdf"));
    }

    private static Document CreateDocument() => Document.Create(
        _syntheticPath,
        DocumentType.Payslip,
        ContentHash.FromBytes("synthetic fixture content"u8),
        Corpus.Personal,
        _ingestedAt);
}
