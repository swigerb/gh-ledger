using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CopilotLedger.Core.Pricing;

namespace CopilotLedger.Core.Telemetry;

// The identity that a live span and an imported session-log record of the same GitHub-billed model call share.
public static class UsageIdentity
{
    public const string GitHubProviderName = "github";

    // The lowercase "D" form of a service request ID: 36 hex digits and hyphens that Guid.TryParseExact(value, "D")
    // accepts, other than the nil UUID. null for anything else.
    public static string? NormalizeServiceRequestId(string? value)
    {
        if (value is not { Length: 36 } ||
            !value.All(character => character == '-' || char.IsAsciiHexDigit(character)) ||
            !Guid.TryParseExact(value, "D", out Guid id) ||
            id == Guid.Empty)
        {
            return null;
        }

        return id.ToString("D");
    }

    // canonical_usage_id: the lowercase hex SHA-256 of ("gh-ledger-usage", "v1", "github-service-request", the normalized
    // service request ID). null unless channel is GitHubAiCredits, providerName is exactly "github", and the ID normalizes,
    // so a BYOK, local, or other provider's request never has one.
    public static string? CanonicalUsageId(string? providerName, string? serviceRequestId, BillingChannel channel)
    {
        string? id = NormalizeServiceRequestId(serviceRequestId);
        return channel == BillingChannel.GitHubAiCredits &&
               string.Equals(providerName, GitHubProviderName, StringComparison.Ordinal) &&
               id is not null
            ? Sha256Hex("gh-ledger-usage", "v1", "github-service-request", id)
            : null;
    }

    // The lowercase hex SHA-256 of the parts, each written as its UTF-8 byte count (4 bytes, big-endian) and then its UTF-8
    // bytes, so two different lists of parts never hash the same bytes.
    public static string Sha256Hex(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> length = stackalloc byte[4];
        foreach (string part in parts)
        {
            ArgumentNullException.ThrowIfNull(part, nameof(parts));
            byte[] bytes = Encoding.UTF8.GetBytes(part);
            BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
