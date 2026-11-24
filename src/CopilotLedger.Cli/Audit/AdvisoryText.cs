using System.Text;

namespace CopilotLedger.Cli.Audit;

// What a local model's answer may put on a terminal: no escape sequences, controls, or bidirectional overrides. Public for
// tests.
public static class AdvisoryText
{
    public const string TruncationLine = "[truncated at 16 KiB]";

    // 1. Drops escape sequences: ESC or a C1 introducer (U+009B CSI, U+009D OSC, U+0090 DCS, U+0098 SOS, U+009E PM, U+009F
    //    APC). CSI: its parameter (U+0030-U+003F) and intermediate (U+0020-U+002F) characters and one final character
    //    (U+0040-U+007E). OSC, DCS, SOS, PM, APC: everything through BEL, ST (ESC \ or U+009C), or the end. ESC and an
    //    intermediate (U+0020-U+002F): the intermediates and one final character (U+0030-U+007E). Any other ESC: it and the
    //    next character. 2. CR LF becomes LF. 3. Drops U+0000-U+0008, U+000B-U+001F (a lone CR too), U+007F, U+0080-U+009F,
    //    U+061C, U+200E, U+200F, U+202A-U+202E, and U+2066-U+2069; keeps tab and LF. 4. A lone surrogate becomes U+FFFD.
    public static string Sanitize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        StringBuilder result = new(text.Length);
        int index = 0;
        while (index < text.Length)
        {
            char character = text[index];
            char? kind = character switch
            {
                '\u001B' => index + 1 < text.Length ? text[index + 1] : null,
                '\u009B' => '[',
                '\u009D' => ']',
                '\u0090' => 'P',
                '\u0098' => 'X',
                '\u009E' => '^',
                '\u009F' => '_',
                _ => null,
            };
            if (character == '\u001B' || kind is not null)
            {
                index = kind is char sequence ? Skip(text, sequence, index + (character == '\u001B' ? 2 : 1)) : index + 1;
                continue;
            }

            if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                result.Append('\n');
                index += 2;
                continue;
            }

            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                result.Append(character).Append(text[index + 1]);
                index += 2;
                continue;
            }

            if (char.IsSurrogate(character))
            {
                result.Append('\uFFFD');
            }
            else if (!IsDropped(character))
            {
                result.Append(character);
            }

            index++;
        }

        return result.ToString();
    }

    // text when its UTF-8 length is at most maxBytes; else its longest prefix of whole runes within maxBytes, "\n", and
    // TruncationLine.
    public static string Cap(string text, int maxBytes)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
        {
            return text;
        }

        int bytes = 0;
        int length = 0;
        foreach (Rune rune in text.EnumerateRunes())
        {
            if (bytes + rune.Utf8SequenceLength > maxBytes)
            {
                break;
            }

            bytes += rune.Utf8SequenceLength;
            length += rune.Utf16SequenceLength;
        }

        return text[..length] + "\n" + TruncationLine;
    }

    // A label for display or the payload: null stays null; else Line, trimmed and cut to maxLength UTF-16 code units, one
    // fewer when the cut would split a surrogate pair.
    public static string? Label(string? value, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        string text = Line(value).Trim();
        if (text.Length <= maxLength)
        {
            return text;
        }

        int cut = char.IsHighSurrogate(text[maxLength - 1]) ? maxLength - 1 : maxLength;
        return text[..cut];
    }

    // A ledger or option string for one cell or line of a console table: Sanitize, then tabs and line feeds become spaces.
    public static string Line(string text) => Sanitize(text).Replace('\t', ' ').Replace('\n', ' ');

    // The end of the escape sequence of kind whose next character is at start.
    private static int Skip(string text, char kind, int start)
    {
        int index = start;
        switch (kind)
        {
            case '[':
                while (index < text.Length && text[index] is >= '\u0030' and <= '\u003F')
                {
                    index++;
                }

                while (index < text.Length && text[index] is >= '\u0020' and <= '\u002F')
                {
                    index++;
                }

                return index < text.Length && text[index] is >= '\u0040' and <= '\u007E' ? index + 1 : index;
            case ']' or 'P' or 'X' or '^' or '_':
                while (index < text.Length)
                {
                    if (text[index] is '\u0007' or '\u009C')
                    {
                        return index + 1;
                    }

                    if (text[index] == '\u001B' && index + 1 < text.Length && text[index + 1] == '\\')
                    {
                        return index + 2;
                    }

                    index++;
                }

                return index;
            case >= '\u0020' and <= '\u002F':
                while (index < text.Length && text[index] is >= '\u0020' and <= '\u002F')
                {
                    index++;
                }

                return index < text.Length && text[index] is >= '\u0030' and <= '\u007E' ? index + 1 : index;
            default:
                return start;
        }
    }

    private static bool IsDropped(char character) =>
        character is (>= '\u0000' and <= '\u0008') or (>= '\u000B' and <= '\u001F') or '\u007F' or (>= '\u0080' and <= '\u009F')
            or '\u061C' or '\u200E' or '\u200F' or (>= '\u202A' and <= '\u202E') or (>= '\u2066' and <= '\u2069');
}
