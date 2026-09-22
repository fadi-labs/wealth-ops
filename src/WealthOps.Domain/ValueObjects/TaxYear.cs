namespace WealthOps.Domain.ValueObjects;

/// <summary>
/// A calendar tax year.
/// </summary>
/// <remarks>
/// Wrapped rather than left as a bare <see cref="int"/> because tax years and ordinals are both
/// small integers that flow through the same signatures, and swapping them silently produces a
/// plausible-looking wrong answer. Rates, thresholds and allowances are keyed by this value
/// (BR-4), so getting it wrong moves money between years.
/// </remarks>
public readonly record struct TaxYear : IComparable<TaxYear>
{
    // Not a guess at the operator's situation — just a sanity window wide enough to admit any
    // real assessment while still catching an ordinal or a two-digit year passed by mistake.
    private const int MinimumYear = 1903;
    private const int MaximumYear = 2200;

    private TaxYear(int value) => Value = value;

    public int Value { get; }

    /// <exception cref="ArgumentOutOfRangeException">The year falls outside the plausible window.</exception>
    public static TaxYear Create(int value)
    {
        if (value is < MinimumYear or > MaximumYear)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Tax year must be between {MinimumYear} and {MaximumYear}.");
        }

        return new TaxYear(value);
    }

    public static bool TryCreate(int value, out TaxYear taxYear)
    {
        if (value is < MinimumYear or > MaximumYear)
        {
            taxYear = default;
            return false;
        }

        taxYear = new TaxYear(value);
        return true;
    }

    public int CompareTo(TaxYear other) => Value.CompareTo(other.Value);

    public static bool operator <(TaxYear left, TaxYear right) => left.Value < right.Value;

    public static bool operator >(TaxYear left, TaxYear right) => left.Value > right.Value;

    public static bool operator <=(TaxYear left, TaxYear right) => left.Value <= right.Value;

    public static bool operator >=(TaxYear left, TaxYear right) => left.Value >= right.Value;

    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
