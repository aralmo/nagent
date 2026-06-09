using System.Globalization;
using System.Text;

namespace Nagent.Core.Shell;

public static class CommandUnescape
{
    public static string UnescapeFully(string input)
    {
        var current = input;
        for (var i = 0; i < 4; i++)
        {
            var next = Unescape(current);
            if (next == current)
            {
                break;
            }

            current = next;
        }

        return current;
    }

    public static string Unescape(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var builder = new StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] != '\\' || i + 1 >= input.Length)
            {
                builder.Append(input[i]);
                continue;
            }

            if (input[i + 1] == 'u' && TryReadUnicodeEscape(input, i + 2, out var codePoint, out var consumed))
            {
                builder.Append((char)codePoint);
                i += consumed;
                continue;
            }

            switch (input[i + 1])
            {
                case 'n':
                    builder.Append('\n');
                    i++;
                    break;
                case 't':
                    builder.Append('\t');
                    i++;
                    break;
                case 'r':
                    builder.Append('\r');
                    i++;
                    break;
                case '\\':
                    builder.Append('\\');
                    i++;
                    break;
                case '"':
                    builder.Append('"');
                    i++;
                    break;
                case '\'':
                    builder.Append('\'');
                    i++;
                    break;
                default:
                    builder.Append(input[i]);
                    break;
            }
        }

        return builder.ToString();
    }

    private static bool TryReadUnicodeEscape(string input, int start, out int codePoint, out int consumed)
    {
        consumed = 0;
        codePoint = 0;

        if (start + 4 > input.Length)
        {
            return false;
        }

        var hex = input.AsSpan(start, 4);
        if (!ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        codePoint = value;
        consumed = 5;
        return true;
    }
}
