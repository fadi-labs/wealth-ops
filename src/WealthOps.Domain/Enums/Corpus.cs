namespace WealthOps.Domain.Enums;

/// <summary>
/// The retrieval partition a document and its chunks belong to.
/// </summary>
/// <remarks>
/// This discriminator is the enforcement point for data locality (BR-1, EP-4), which is why it
/// exists from M0 even though v1 has no remote path and no third corpus. Similarity search is
/// always scoped to exactly one corpus — <see cref="Rules"/> content is public and may be shared
/// or debugged freely, <see cref="Personal"/> content may not leave the machine. A search that
/// could span both would erase the distinction the rest of the system is built to preserve.
/// </remarks>
public enum Corpus
{
    /// <summary>Public tax-rule material. Agent-readable, cached outside the personal data root.</summary>
    Rules = 1,

    /// <summary>The operator's own documents. Never leaves the local machine.</summary>
    Personal = 2
}
