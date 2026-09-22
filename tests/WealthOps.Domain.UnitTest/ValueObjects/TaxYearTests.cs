using WealthOps.Domain.ValueObjects;

namespace WealthOps.Domain.UnitTest.ValueObjects;

public sealed class TaxYearTests
{
    [Fact]
    public void Create_CarriesTheYear()
        => TaxYear.Create(2026).Value.ShouldBe(2026);

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    [InlineData(1899)]
    [InlineData(9999)]
    public void Create_RejectsImplausibleYears(int value)
    {
        // The window is wide enough to admit any real assessment and narrow enough to catch an
        // ordinal or a two-digit year passed where a tax year was expected.
        Should.Throw<ArgumentOutOfRangeException>(() => TaxYear.Create(value));
    }

    [Fact]
    public void TryCreate_ReturnsFalseWithoutThrowing()
    {
        TaxYear.TryCreate(26, out TaxYear taxYear).ShouldBeFalse();
        taxYear.ShouldBe(default);
    }

    [Fact]
    public void Comparison_OrdersAdjacentYears()
    {
        TaxYear earlier = TaxYear.Create(2025);
        TaxYear later = TaxYear.Create(2026);

        // Two adjacent years are held concurrently (BR-4), so ordering between them is load-bearing.
        (earlier < later).ShouldBeTrue();
        (later > earlier).ShouldBeTrue();
        earlier.CompareTo(later).ShouldBeLessThan(0);
    }

    [Fact]
    public void Equality_IsByValue()
        => TaxYear.Create(2026).ShouldBe(TaxYear.Create(2026));
}
