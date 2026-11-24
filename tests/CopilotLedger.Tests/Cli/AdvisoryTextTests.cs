using System.Globalization;
using System.Text;
using CopilotLedger.Cli.Audit;

namespace CopilotLedger.Tests.Cli;

// AdvisoryText: what a local model's answer may put on a terminal. Inputs are written with \e (ESC), \a (BEL), \r, \n, \t,
// \0, and \uXXXX escapes, decoded by Decode.
public sealed class AdvisoryTextTests
{
    public static TheoryData<string, string> Sanitized => new()
    {
        { @"Use \e[31mred\e[0m text", "Use red text" },
        { @"a\e]8;;https://example.com\alink\e]8;;\ab", "alinkb" },
        { @"a\e]0;title\e\\b", "ab" },
        { @"x\u009B31my", "xy" },
        { @"\ePq#0;1\e\\done", "done" },
        { @"\e(Bascii", "ascii" },
        { @"\e7save\e8", "save" },
        { @"line1\r\nline2\rline3", @"line1\nline2line3" },
        { @"tab\there\n", @"tab\there\n" },
        { @"bidi\u202Etxt\u2066x\u2069\u200Fy", "biditxtxy" },
        { @"nul\0del\u007Fc1\u0085", "nuldelc1" },
        { @"\uD800x\uDC00", @"\uFFFDx\uFFFD" },
        { @"abc\e", "abc" },
        { @"emoji \uD83D\uDE00 \u00E9", @"emoji \uD83D\uDE00 \u00E9" },
    };

    [Theory]
    [MemberData(nameof(Sanitized))]
    public void LT1_Sanitize_DropsEscapeSequencesControlsAndBidiOverrides(string input, string expected)
    {
        Assert.Equal(Decode(expected), AdvisoryText.Sanitize(Decode(input)));
    }

    public static TheoryData<string, string> Capped => new()
    {
        { "16384 a", "16384 a" },
        { "16385 a", "16384 a + line" },
        { "8193 é", "8192 é + line" },
        { "a + 4096 emoji", "a + 4095 emoji + line" },
    };

    [Theory]
    [MemberData(nameof(Capped))]
    public void LT2_Cap_CutsOnARuneBoundaryAndSaysSo(string input, string expected)
    {
        Assert.Equal(Build(expected), AdvisoryText.Cap(Build(input), 16_384));

        static string Build(string shape) => shape switch
        {
            "16384 a" => new string('a', 16_384),
            "16385 a" => new string('a', 16_385),
            "16384 a + line" => new string('a', 16_384) + "\n[truncated at 16 KiB]",
            "8193 é" => new string('é', 8_193),
            "8192 é + line" => new string('é', 8_192) + "\n[truncated at 16 KiB]",
            "a + 4096 emoji" => "a" + string.Concat(Enumerable.Repeat("\uD83D\uDE00", 4_096)),
            "a + 4095 emoji + line" => "a" + string.Concat(Enumerable.Repeat("\uD83D\uDE00", 4_095)) + "\n[truncated at 16 KiB]",
            _ => throw new ArgumentOutOfRangeException(nameof(shape)),
        };
    }

    public static TheoryData<string?, string?> Labels => new()
    {
        { null, null },
        { @"  ex\e[1mplore\tbot\n ", "explore bot" },
        { new string('a', 63) + @"\uD83D\uDE00", new string('a', 63) },
        { new string('x', 100), new string('x', 64) },
    };

    [Theory]
    [MemberData(nameof(Labels))]
    public void LT3_Label_IsSanitizedOnOneLineAndCut(string? input, string? expected)
    {
        Assert.Equal(expected, AdvisoryText.Label(input is null ? null : Decode(input), 64));
    }

    // input with \e, \a, \r, \n, \t, \0, \\, and \uXXXX replaced by the characters they name.
    private static string Decode(string input)
    {
        StringBuilder result = new();
        for (int index = 0; index < input.Length; index++)
        {
            if (input[index] != '\\' || index + 1 == input.Length)
            {
                result.Append(input[index]);
                continue;
            }

            char code = input[++index];
            if (code == 'u')
            {
                result.Append((char)int.Parse(input.AsSpan(index + 1, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                index += 4;
                continue;
            }

            result.Append(code switch
            {
                'e' => '\u001B',
                'a' => '\u0007',
                'r' => '\r',
                'n' => '\n',
                't' => '\t',
                '0' => '\0',
                _ => code,
            });
        }

        return result.ToString();
    }
}
