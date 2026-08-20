using System.Globalization;
using System.IO;

namespace Jint.Profiling;

/// <summary>
/// Renders a <see cref="ScriptProfile"/> as a speedscope <c>evented</c> profile, per
/// <see href="https://www.speedscope.app/file-format-schema.json">the published schema</see>: a
/// <c>FileFormat.File</c> whose required members are <c>$schema</c>, <c>shared.frames</c> and
/// <c>profiles</c>, carrying a single <c>FileFormat.EventedProfile</c> whose required members are
/// <c>type</c>, <c>name</c>, <c>unit</c>, <c>startValue</c>, <c>endValue</c> and <c>events</c>, each event
/// being an <c>{ type: "O" | "C", frame, at }</c>.
/// </summary>
/// <remarks>
/// Written by hand rather than through a JSON library because Jint takes no dependency it does not need,
/// and because the document's shape is fixed: the only values that vary are two integers per event, four
/// members per frame, and the strings among them.
/// </remarks>
internal static class SpeedscopeWriter
{
    private const string SchemaUrl = "https://www.speedscope.app/file-format-schema.json";
    private const string ProfileName = "Jint";

    internal static void Write(
        TextWriter writer,
        ScriptProfileFrame[] frames,
        ScriptProfileEvent[] events,
        long durationNanoseconds)
    {
        writer.Write("{\"$schema\":\"");
        writer.Write(SchemaUrl);
        writer.Write("\",\"exporter\":\"Jint\",\"name\":\"");
        writer.Write(ProfileName);
        writer.Write("\",\"activeProfileIndex\":0,\"shared\":{\"frames\":[");

        for (var i = 0; i < frames.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            WriteFrame(writer, in frames[i]);
        }

        writer.Write("]},\"profiles\":[{\"type\":\"evented\",\"name\":\"");
        writer.Write(ProfileName);
        writer.Write("\",\"unit\":\"nanoseconds\",\"startValue\":0,\"endValue\":");
        WriteNumber(writer, durationNanoseconds);
        writer.Write(",\"events\":[");

        for (var i = 0; i < events.Length; i++)
        {
            if (i > 0)
            {
                writer.Write(',');
            }

            var e = events[i];
            writer.Write(e.Kind == ScriptProfileEventKind.Open ? "{\"type\":\"O\",\"frame\":" : "{\"type\":\"C\",\"frame\":");
            WriteNumber(writer, e.FrameIndex);
            writer.Write(",\"at\":");
            WriteNumber(writer, e.TimestampNanoseconds);
            writer.Write('}');
        }

        writer.Write("]}]}");
    }

    private static void WriteFrame(TextWriter writer, in ScriptProfileFrame frame)
    {
        writer.Write("{\"name\":");
        WriteString(writer, frame.Name);

        if (frame.File is not null)
        {
            writer.Write(",\"file\":");
            WriteString(writer, frame.File);
        }

        if (frame.Line is { } line)
        {
            writer.Write(",\"line\":");
            WriteNumber(writer, line);
        }

        if (frame.Column is { } column)
        {
            writer.Write(",\"col\":");
            WriteNumber(writer, column);
        }

        writer.Write('}');
    }

    /// <summary>
    /// Numbers go through <see cref="CultureInfo.InvariantCulture"/> explicitly rather than through
    /// <c>TextWriter.Write(long)</c>, which formats with the writer's own provider — the current culture
    /// for a <see cref="StreamWriter"/>.
    /// </summary>
    private static void WriteNumber(TextWriter writer, long value)
    {
        writer.Write(value.ToString(CultureInfo.InvariantCulture));
    }

    private static void WriteString(TextWriter writer, string value)
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
