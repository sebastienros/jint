using System.Globalization;
using System.IO;

namespace Jint.Profiling;

/// <summary>
/// The three primitives both profile writers need. Written by hand rather than through a JSON library
/// because Jint takes no dependency it does not need, and because both documents' shapes are fixed: the
/// only values that vary are numbers, and the strings among them.
/// </summary>
internal static class ProfileJson
{
    /// <summary>
    /// Numbers go through <see cref="CultureInfo.InvariantCulture"/> explicitly rather than through
    /// <c>TextWriter.Write(long)</c>, which formats with the writer's own provider — the current culture
    /// for a <see cref="StreamWriter"/>.
    /// </summary>
    internal static void WriteNumber(TextWriter writer, long value)
    {
        writer.Write(value.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// A millisecond timestamp, to four decimals. A fixed-point format rather than the default one: a
    /// round-trip format is allowed to produce exponent notation, which is valid JSON but which not every
    /// profile reader accepts, and four decimals is a tenth of a microsecond — finer than anything a
    /// statement-cadence sampler can resolve.
    /// </summary>
    internal static void WriteMilliseconds(TextWriter writer, double value)
    {
        writer.Write(value.ToString("0.####", CultureInfo.InvariantCulture));
    }

    internal static void WriteString(TextWriter writer, string value)
    {
        writer.Write('"');

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '"':
                    writer.Write("\\\"");
                    break;
                case '\\':
                    writer.Write("\\\\");
                    break;
                case '\b':
                    writer.Write("\\b");
                    break;
                case '\f':
                    writer.Write("\\f");
                    break;
                case '\n':
                    writer.Write("\\n");
                    break;
                case '\r':
                    writer.Write("\\r");
                    break;
                case '\t':
                    writer.Write("\\t");
                    break;
                default:
                    if (c < ' ')
                    {
                        WriteEscaped(writer, c);
                    }
                    else if (char.IsSurrogate(c))
                    {
                        // A JS function name is a string of UTF-16 code units and may hold an unpaired
                        // surrogate, which has no UTF-8 encoding; escaping it keeps the document decodable.
                        if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                        {
                            writer.Write(c);
                            writer.Write(value[i + 1]);
                            i++;
                        }
                        else
                        {
                            WriteEscaped(writer, c);
                        }
                    }
                    else
                    {
                        writer.Write(c);
                    }

                    break;
            }
        }

        writer.Write('"');
    }

    private static void WriteEscaped(TextWriter writer, char c)
    {
        writer.Write("\\u");
        writer.Write(((int) c).ToString("x4", CultureInfo.InvariantCulture));
    }
}
