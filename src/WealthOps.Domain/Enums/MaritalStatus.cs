namespace WealthOps.Domain.Enums;

/// <summary>
/// Whether Danish rules treat this taxpayer as assessed alone or as part of a couple.
/// </summary>
/// <remarks>
/// <see cref="Married"/> is not a biographical detail — it changes the assessment: unused
/// personfradrag transfers between spouses and the aktieindkomst progression threshold doubles
/// (BR-5). Which taxpayer is which is configuration, never documented here (BR-16).
/// </remarks>
public enum MaritalStatus
{
    Single = 1,
    Married = 2
}
