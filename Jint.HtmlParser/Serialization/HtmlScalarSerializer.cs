namespace Jint.HtmlParser.Serialization;

// HTML Standard §13.3, https://html.spec.whatwg.org/multipage/parsing.html#serialising-html-fragments.
internal static class HtmlScalarSerializer
{
    internal static void WriteText(Text text, bool raw, SerializationWriter writer)
    {
        var work = writer.Work;
        Span<char> buffer = stackalloc char[256];
        var buffered = 0;
        for (var index = 0; index < text.DataLength; index++)
        {
            work.Charge(1, SerializationStage.HtmlEscape);
            var character = text.DataAt(index);
            var replacement = raw ? null : character switch
            {
                '&' => "&amp;",
                '\u00a0' => "&nbsp;",
                '<' => "&lt;",
                '>' => "&gt;",
                _ => null
            };
            if (replacement is not null)
            {
                if (buffered != 0) writer.Append(buffer[..buffered]);
                buffered = 0;
                writer.Append(replacement);
            }
            else
            {
                buffer[buffered++] = character;
                if (buffered != buffer.Length) continue;
                writer.Append(buffer);
                buffered = 0;
            }
        }
        if (buffered != 0) writer.Append(buffer[..buffered]);
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
        var runStart = 0;
        for (var index = 0; index < value.Length; index++)
        {
            work.Charge(1, SerializationStage.HtmlEscape);
            var replacement = value[index] switch
            {
                '&' => "&amp;",
                '\u00a0' => "&nbsp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' when attribute => "&quot;",
                _ => null
            };
            if (replacement is null) continue;
            if (index != runStart) writer.Append(value.Slice(runStart, index - runStart));
            writer.Append(replacement);
            runStart = index + 1;
        }

        if (runStart != value.Length) writer.Append(value[runStart..]);
    }

    internal static void WriteLiteral(string value, SerializationWriter writer, SerializationStage stage)
    {
        ArgumentNullException.ThrowIfNull(value);
        var work = writer.Work;
        for (var offset = 0; offset < value.Length;)
        {
            var count = Math.Min(256, value.Length - offset);
            work.Charge(count, stage);
            writer.Append(value.AsSpan(offset, count));
            offset += count;
        }
    }
}
