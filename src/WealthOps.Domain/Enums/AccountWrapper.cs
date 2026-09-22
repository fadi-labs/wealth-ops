namespace WealthOps.Domain.Enums;

/// <summary>
/// The tax wrapper an account sits in, which determines how its holdings are assessed.
/// </summary>
/// <remarks>
/// Economically identical holdings are taxed differently depending on this value, so it is a
/// first-class discriminator rather than a label: <see cref="Aktiesparekonto"/> is lager-taxed at
/// account level on the annual change in value, while <see cref="FrieMidler"/> is realisation-taxed
/// per disposal — except for lager-classified instruments held inside it, where the classification
/// follows the instrument rather than the wrapper (DS-17).
/// </remarks>
public enum AccountWrapper
{
    /// <summary>Share savings account — flat-rate annual tax on the change in value.</summary>
    Aktiesparekonto = 1,

    /// <summary>An ordinary unwrapped investment account.</summary>
    FrieMidler = 2
}
