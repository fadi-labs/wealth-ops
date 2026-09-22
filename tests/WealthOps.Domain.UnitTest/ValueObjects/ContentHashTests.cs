using System.Text;
using WealthOps.Domain.ValueObjects;

namespace WealthOps.Domain.UnitTest.ValueObjects;

public sealed class ContentHashTests
{
    [Fact]
    public void FromBytes_ProducesSixtyFourLowercaseHexCharacters()
    {
        ContentHash hash = ContentHash.FromBytes("synthetic fixture content"u8);

        hash.Value.Length.ShouldBe(64);
        hash.Value.ShouldBe(hash.Value.ToLowerInvariant());
        hash.Value.ShouldAllBe(c => Uri.IsHexDigit(c));
    }

    [Fact]
    public void FromBytes_IsStableForIdenticalContent()
    {
        ContentHash first = ContentHash.FromBytes("synthetic fixture content"u8);
        ContentHash second = ContentHash.FromBytes("synthetic fixture content"u8);

        // The guarantee behind BR-11: identical content is identical identity, so re-ingesting
        // an unchanged file cannot produce a second document.
        second.ShouldBe(first);
    }

    [Fact]
    public void FromBytes_DiffersForDifferentContent()
    {
        ContentHash first = ContentHash.FromBytes("synthetic fixture content"u8);
        ContentHash second = ContentHash.FromBytes("synthetic fixture content."u8);

        second.ShouldNotBe(first);
    }

    [Fact]
    public async Task FromStreamAsync_MatchesFromBytesForTheSameContent()
    {
        byte[] content = Encoding.UTF8.GetBytes("synthetic fixture content");

        using var stream = new MemoryStream(content);
        ContentHash fromStream = await ContentHash.FromStreamAsync(stream, TestContext.Current.CancellationToken);

        fromStream.ShouldBe(ContentHash.FromBytes(content));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    public void Parse_RejectsAnythingThatIsNotASha256HexDigest(string value)
        => Should.Throw<ArgumentException>(() => ContentHash.Parse(value));

    [Fact]
    public void Parse_RejectsUppercaseHex()
    {
        string upper = ContentHash.FromBytes("synthetic"u8).Value.ToUpperInvariant();

        // One canonical form only. Two spellings of the same hash would defeat the unique index
        // that makes re-ingestion idempotent.
        Should.Throw<ArgumentException>(() => ContentHash.Parse(upper));
    }

    [Fact]
    public void Parse_RoundTripsAStoredValue()
    {
        ContentHash original = ContentHash.FromBytes("synthetic fixture content"u8);

        ContentHash.Parse(original.Value).ShouldBe(original);
    }

    [Fact]
    public void TryParse_ReturnsFalseWithoutThrowing()
    {
        ContentHash.TryParse("not-a-hash", out ContentHash hash).ShouldBeFalse();
        hash.ShouldBe(default);
    }
}
