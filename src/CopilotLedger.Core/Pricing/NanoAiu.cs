using System.Text.Json;

namespace CopilotLedger.Core.Pricing;

public static class NanoAiu
{
    public const long MaxExact = 9_007_199_254_740_992;

    public static bool TryFromJsonNumber(ReadOnlySpan<byte> utf8Json, out long nanoAiu)
    {
        nanoAiu = 0;
        Utf8JsonReader reader = new(utf8Json);
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number)
        {
            return false;
        }

        if (!reader.TryGetDecimal(out decimal value))
        {
            return false;
        }

        if (value < 0 || value != decimal.Truncate(value) || value > MaxExact)
        {
            return false;
        }

        if (value == 0)
        {
            ReadOnlySpan<byte> mantissa = reader.ValueSpan;
            int exponentIndex = mantissa.IndexOfAny((byte)'e', (byte)'E');
            if (exponentIndex >= 0)
            {
                mantissa = mantissa[..exponentIndex];
            }

            foreach (byte valueByte in mantissa)
            {
                if (valueByte is >= (byte)'1' and <= (byte)'9')
                {
                    return false;
                }
            }
        }

        nanoAiu = (long)value;
        return true;
    }

    public static bool TryFromIeee754Bits(ulong bits, out long nanoAiu)
    {
        nanoAiu = 0;
        int exponent = (int)((bits >> 52) & 0x7FF);
        ulong mantissa = bits & 0x000F_FFFF_FFFF_FFFF;
        bool negative = (bits >> 63) != 0;

        if (exponent == 0x7FF)
        {
            return false;
        }

        if (exponent == 0 && mantissa == 0)
        {
            return true;
        }

        if (exponent == 0 || negative)
        {
            return false;
        }

        ulong significand = mantissa | (1UL << 52);
        int shift = exponent - 1075;
        if (shift >= 0)
        {
            if (shift > 1)
            {
                return false;
            }

            ulong value = significand << shift;
            if (value > (ulong)MaxExact)
            {
                return false;
            }

            nanoAiu = (long)value;
            return true;
        }

        if (shift <= -53)
        {
            return false;
        }

        ulong low = significand & ((1UL << -shift) - 1);
        if (low != 0)
        {
            return false;
        }

        nanoAiu = (long)(significand >> -shift);
        return true;
    }

    public static decimal ToUsd(long nanoAiu)
    {
        NanoAiuContracts.Validate(nanoAiu);
        return nanoAiu * 0.00000000001m;
    }

    public static decimal ToAiCredits(long nanoAiu)
    {
        NanoAiuContracts.Validate(nanoAiu);
        return nanoAiu / 1_000_000_000m;
    }

    public static long ToPicoUsd(long nanoAiu)
    {
        NanoAiuContracts.Validate(nanoAiu);
        return checked(nanoAiu * 10);
    }
}

internal static class NanoAiuContracts
{
    public static void Validate(long nanoAiu)
    {
        if (nanoAiu < 0 || nanoAiu > NanoAiu.MaxExact)
        {
            throw new ArgumentOutOfRangeException(nameof(nanoAiu));
        }
    }
}
