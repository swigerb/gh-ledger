using System.Diagnostics.CodeAnalysis;

namespace CopilotLedger.Core.Import;

// A producer's MAJOR.MINOR.PATCH version. A prerelease or build suffix is accepted and ignored when comparing, so
// 1.0.106-8.unstable.r43566476370.g869eef3 is 1.0.106.
public sealed record ProducerVersion(int Major, int Minor, int Patch) : IComparable<ProducerVersion>
{
    public const int MaxLength = 64;

    // MAJOR.MINOR.PATCH, each "0" or 1 to 9 digits without a leading zero, then optionally "-" or "+" and at least one of
    // [0-9A-Za-z.+-]; at most 64 characters in all.
    public static bool TryParse(string? value, [NotNullWhen(true)] out ProducerVersion? version)
    {
        version = null;
        if (value is null || value.Length is 0 or > MaxLength)
        {
            return false;
        }

        int position = 0;
        int[] parts = new int[3];
        for (int part = 0; part < 3; part++)
        {
            int start = position;
            while (position < value.Length && char.IsAsciiDigit(value[position]))
            {
                position++;
            }

            int length = position - start;
            if (length is 0 or > 9 || (length > 1 && value[start] == '0'))
            {
                return false;
            }

            parts[part] = int.Parse(value.AsSpan(start, length), System.Globalization.CultureInfo.InvariantCulture);
            if (part < 2)
            {
                if (position >= value.Length || value[position] != '.')
                {
                    return false;
                }

                position++;
            }
        }

        if (position < value.Length)
        {
            if (value[position] is not ('-' or '+') ||
                position + 1 == value.Length ||
                value.AsSpan(position + 1).ContainsAnyExcept(SuffixCharacters))
            {
                return false;
            }
        }

        version = new ProducerVersion(parts[0], parts[1], parts[2]);
        return true;
    }

    public int CompareTo(ProducerVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        int major = Major.CompareTo(other.Major);
        if (major != 0)
        {
            return major;
        }

        int minor = Minor.CompareTo(other.Minor);
        return minor != 0 ? minor : Patch.CompareTo(other.Patch);
    }

    public override string ToString() =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Major}.{Minor}.{Patch}");

    private static readonly System.Buffers.SearchValues<char> SuffixCharacters = System.Buffers.SearchValues.Create(
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz.+-");
}
