using WealthOps.Domain.ValueObjects;

namespace WealthOps.Domain.UnitTest.ValueObjects;

public sealed class EmbeddingVectorTests
{
    [Fact]
    public void Create_ExposesItsOwnDimension()
    {
        EmbeddingVector vector = EmbeddingVector.Create([0.1f, 0.2f, 0.3f, 0.4f]);

        // The dimension is a property of the value, not a global constant (ADR-002).
        vector.Dimensions.ShouldBe(4);
    }

    [Fact]
    public void Create_RejectsAnEmptyVector()
        => Should.Throw<ArgumentException>(() => EmbeddingVector.Create(Array.Empty<float>()));

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void Create_RejectsNonFiniteComponents(float bad)
    {
        // A broken embedding response, not an unusual one. Storing it would poison every later
        // similarity search silently (NFR-7).
        Should.Throw<ArgumentException>(() => EmbeddingVector.Create([0.1f, bad, 0.3f]));
    }

    [Fact]
    public void Create_CopiesTheSuppliedArray()
    {
        float[] source = [0.1f, 0.2f, 0.3f];
        EmbeddingVector vector = EmbeddingVector.Create(source);

        source[0] = 99f;

        // The caller keeps no handle on the vector's state.
        vector.ToArray()[0].ShouldBe(0.1f);
    }

    [Fact]
    public void ToArray_DoesNotExposeInternalState()
    {
        EmbeddingVector vector = EmbeddingVector.Create([0.1f, 0.2f, 0.3f]);

        float[] copy = vector.ToArray();
        copy[0] = 99f;

        vector.ToArray()[0].ShouldBe(0.1f);
    }

    [Fact]
    public void Equality_IsByValue()
    {
        EmbeddingVector first = EmbeddingVector.Create([0.1f, 0.2f, 0.3f]);
        EmbeddingVector second = EmbeddingVector.Create([0.1f, 0.2f, 0.3f]);

        second.ShouldBe(first);
        second.GetHashCode().ShouldBe(first.GetHashCode());
    }

    [Fact]
    public void Equality_DistinguishesDifferentWidths()
    {
        EmbeddingVector narrow = EmbeddingVector.Create([0.1f, 0.2f]);
        EmbeddingVector wide = EmbeddingVector.Create([0.1f, 0.2f, 0.3f]);

        wide.ShouldNotBe(narrow);
    }

    [Fact]
    public void Create_FromSpan_MatchesTheArrayOverload()
    {
        ReadOnlySpan<float> span = [0.1f, 0.2f, 0.3f];

        EmbeddingVector.Create(span).ShouldBe(EmbeddingVector.Create([0.1f, 0.2f, 0.3f]));
    }
}
