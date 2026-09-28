using System.Buffers;

namespace Jint.HtmlParser.Serialization;

// HTML Standard §13.3, https://html.spec.whatwg.org/multipage/parsing.html#serialising-html-fragments.
internal static class HtmlScalarSerializer
{
    private const int ScanSlice = 256;
    private static readonly SearchValues<char> TextEscapes = SearchValues.Create("&<>\u00a0");
    private static readonly SearchValues<char> AttributeEscapes = SearchValues.Create("&<>\"\u00a0");

    internal static void WriteText(Text text, bool raw, SerializationWriter writer)
    {
        if (raw) WriteLiteral(text.DataSpan, writer, SerializationStage.HtmlEscape);
        else WriteEscaped(text.DataSpan, attribute: false, writer);
    }

    internal static void WriteEscaped(string value, bool attribute, SerializationWriter writer)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteEscaped(value.AsSpan(), attribute, writer);
    }

    internal static void WriteEscaped(ReadOnlySpan<char> value, bool attribute, SerializationWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var work = writer.Work;
        var escapes = attribute ? AttributeEscapes : TextEscapes;
        while (!value.IsEmpty)
        {
            // Scan at most one cadence slice so a long run still reaches its escape checkpoints.
            var slice = value.Length > ScanSlice ? value[..ScanSlice] : value;
            var index = slice.IndexOfAny(escapes);
            var run = index < 0 ? slice.Length : index;
            if (run != 0)
            {
                work.Charge(run, SerializationStage.HtmlEscape);
                writer.Append(slice[..run]);
            }
            if (index < 0)
            {
                value = value[run..];
                continue;
            }
            work.Charge(1, SerializationStage.HtmlEscape);
            writer.Append(value[index] switch
            {
                '&' => "&amp;",
                '\u00a0' => "&nbsp;",
                '<' => "&lt;",
                '>' => "&gt;",
                _ => "&quot;"
            });
            value = value[(index + 1)..];
        }
    }

    internal static void WriteLiteral(string value, SerializationWriter writer, SerializationStage stage)
    {
        ArgumentNullException.ThrowIfNull(value);
        WriteLiteral(value.AsSpan(), writer, stage);
    }

    internal static void WriteLiteral(ReadOnlySpan<char> value, SerializationWriter writer, SerializationStage stage)
    {
        var work = writer.Work;
        while (!value.IsEmpty)
        {
            var count = Math.Min(ScanSlice, value.Length);
            work.Charge(count, stage);
            writer.Append(value[..count]);
            value = value[count..];
        }
    }
}
