namespace WealthOps.Domain.ValueObjects;

/// <summary>
/// A dense embedding, held as a plain float array.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a plain array and nothing more. The database representation is a
/// <c>pgvector</c> column, but that type belongs to Infrastructure — importing it here would put a
/// persistence package inside the innermost layer. The EF configuration converts between the two.
/// </para>
/// <para>
/// The dimension is a property of this value, not a global constant, because the embedding model
/// is configuration and may change (ADR-002). A vector always knows how wide it is; nothing else
/// has to assume.
/// </para>
/// </remarks>
public sealed class EmbeddingVector : IEquatable<EmbeddingVector>
{
    private readonly float[] _values;

    private EmbeddingVector(float[] values) => _values = values;

    /// <summary>The number of components. Always at least one.</summary>
    public int Dimensions => _values.Length;

    /// <summary>The components, as a non-mutable view over the backing array.</summary>
    public ReadOnlySpan<float> Values => _values;

    /// <summary>
    /// Creates a vector, rejecting anything that could not have come from a working model.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The vector is empty, or contains a non-finite component. Both indicate a broken embedding
    /// response rather than an unusual one, and storing either would poison every later similarity
    /// search silently (NFR-7).
    /// </exception>
    public static EmbeddingVector Create(ReadOnlySpan<float> values) => Create(values.ToArray());

    /// <inheritdoc cref="Create(ReadOnlySpan{float})"/>
    /// <remarks>
    /// The array overload is the primary one, and exists separately because EF Core value
    /// converters are compiled expression trees, which cannot carry a <see cref="ReadOnlySpan{T}"/>.
    /// The array is copied, so the caller keeps no handle on the vector's state.
    /// </remarks>
    public static EmbeddingVector Create(float[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Length == 0)
        {
            throw new ArgumentException("An embedding must have at least one component.", nameof(values));
        }

        foreach (float value in values)
        {
            if (!float.IsFinite(value))
            {
                throw new ArgumentException(
                    "An embedding must contain only finite components; the model returned NaN or Infinity.",
                    nameof(values));
            }
        }

        return new EmbeddingVector([.. values]);
    }

    /// <summary>Copies the components into a new array.</summary>
    public float[] ToArray() => _values.AsSpan().ToArray();

    public bool Equals(EmbeddingVector? other)
        => other is not null && _values.AsSpan().SequenceEqual(other._values);

    public override bool Equals(object? obj) => Equals(obj as EmbeddingVector);

    public override int GetHashCode()
    {
        // Dimension plus the first and last components: enough to scatter well without walking
        // a 1000-component array on every dictionary probe.
        var hash = new HashCode();
        hash.Add(_values.Length);
        hash.Add(_values[0]);
        hash.Add(_values[^1]);
        return hash.ToHashCode();
    }
}
