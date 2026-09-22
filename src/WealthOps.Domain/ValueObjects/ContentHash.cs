using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

namespace WealthOps.Domain.ValueObjects;

/// <summary>
/// A SHA-256 content hash, held as 64 lowercase hex characters.
/// </summary>
/// <remarks>
/// This is the identity of an ingested file's *content*, and it is what makes re-ingestion
/// idempotent (BR-11, AC-1): re-dropping an unchanged export is a no-op because its hash already
/// exists. Path is deliberately not part of the identity — the same file moved or re-downloaded
/// under a new name is still the same document.
/// </remarks>
public readonly record struct ContentHash
{
    private const int HexLength = 64;

    private ContentHash(string value) => Value = value;

    /// <summary>The hash as 64 lowercase hex characters.</summary>
    public string Value { get; }

    /// <summary>Hashes the supplied bytes.</summary>
    public static ContentHash FromBytes(ReadOnlySpan<byte> content)
    {
        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(content, digest);
        return new ContentHash(Convert.ToHexStringLower(digest));
    }

    /// <summary>Hashes a stream without buffering it in full — ingested files may be large.</summary>
    public static async Task<ContentHash> FromStreamAsync(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        byte[] digest = await SHA256.HashDataAsync(content, cancellationToken).ConfigureAwait(false);
        return new ContentHash(Convert.ToHexStringLower(digest));
    }

    /// <summary>Parses a stored hash, rejecting anything that is not 64 hex characters.</summary>
    /// <exception cref="ArgumentException">The value is not a well-formed SHA-256 hex digest.</exception>
    public static ContentHash Parse(string value)
    {
        if (!TryParse(value, out ContentHash hash))
        {
            throw new ArgumentException(
                $"Content hash must be {HexLength} hexadecimal characters.",
                nameof(value));
        }

        return hash;
    }

    /// <summary>Parses a stored hash, returning <see langword="false"/> rather than throwing.</summary>
    public static bool TryParse([NotNullWhen(true)] string? value, out ContentHash hash)
    {
        hash = default;

        if (value is null || value.Length != HexLength)
        {
            return false;
        }

        foreach (char c in value)
        {
            bool isLowerHex = c is >= '0' and <= '9' or >= 'a' and <= 'f';
            if (!isLowerHex)
            {
                return false;
            }
        }

        hash = new ContentHash(value);
        return true;
    }

    public override string ToString() => Value;
}
