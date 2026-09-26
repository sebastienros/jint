namespace Jint.HtmlParser.Serialization;

// DOM Parsing §5.2.1.1.3 and §§5.2.1.3–5.2.1.8.
// https://w3c.github.io/DOM-Parsing/#xml-serialization
internal static class XmlScalarSerializer
{
    internal static void WriteText(Text node, SerializationWriter writer, bool requireWellFormed)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(writer);
        WriteEscaped(node.Data, writer, requireWellFormed, attribute: false);
    }

    internal static void WriteAttributeValue(string? value, SerializationWriter writer, bool requireWellFormed)
    {
        ArgumentNullException.ThrowIfNull(writer);
        WriteEscaped(value ?? string.Empty, writer, requireWellFormed, attribute: true);
    }

    internal static void WriteComment(Comment node, SerializationWriter writer, bool requireWellFormed)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(writer);
        var data = node.Data;
        if (requireWellFormed)
        {
            ValidateXmlCharacters(data, writer.Work);
            var previous = '\0';
            foreach (var character in data)
            {
                writer.Work.Charge(1, SerializationStage.Scan);
                if (previous == '-' && character == '-') Invalid("An XML comment contains '--'.");
                previous = character;
            }

            if (previous == '-') Invalid("An XML comment ends with '-'.");
        }

        writer.Append("<!--");
        writer.Append(data);
        writer.Append("-->");
    }

    internal static void WriteCData(CDataSection node, SerializationWriter writer, bool requireWellFormed)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(writer);
        // The draft deliberately performs no well-formedness check here, even for mutated data.
        _ = requireWellFormed;
        writer.Append("<![CDATA[");
        writer.Append(node.Data);
        writer.Append("]]>");
    }

    internal static void WriteProcessingInstruction(ProcessingInstruction node, SerializationWriter writer,
        bool requireWellFormed)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(writer);
        var target = node.Target;
        var data = node.Data;
        if (requireWellFormed)
        {
            // DOM creation already checked XML Name; the serializer adds only the specified checks.
            for (var index = 0; index < target.Length; index++)
            {
                writer.Work.Charge(1, SerializationStage.Scan);
                if (target[index] == ':') Invalid("An XML processing instruction target contains ':'.");
            }

            if (target.Length == 3 && AsciiEqualIgnoreCase(target[0], 'x') &&
                AsciiEqualIgnoreCase(target[1], 'm') && AsciiEqualIgnoreCase(target[2], 'l'))
            {
                Invalid("The XML processing instruction target is reserved.");
            }

            ValidateXmlCharacters(data, writer.Work);
            var previous = '\0';
            foreach (var character in data)
            {
                writer.Work.Charge(1, SerializationStage.Scan);
                if (previous == '?' && character == '>') Invalid("An XML processing instruction contains '?>'.");
                previous = character;
            }
        }

        writer.Append("<?");
        writer.Append(target);
        writer.Append(' ');
        writer.Append(data);
        writer.Append("?>");
    }

    internal static void WriteDocumentType(DocumentType node, SerializationWriter writer, bool requireWellFormed)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(writer);
        var publicId = node.PublicId;
        var systemId = node.SystemId;
        if (requireWellFormed)
        {
            foreach (var character in publicId)
            {
                writer.Work.Charge(1, SerializationStage.Scan);
                if (!IsPubidChar(character)) Invalid("The XML doctype public identifier contains an invalid character.");
            }

            ValidateXmlCharacters(systemId, writer.Work);
            var doubleQuote = false;
            var singleQuote = false;
            foreach (var character in systemId)
            {
                writer.Work.Charge(1, SerializationStage.Scan);
                doubleQuote |= character == '"';
                singleQuote |= character == '\'';
                if (doubleQuote && singleQuote) Invalid("The XML doctype system identifier contains both quote kinds.");
            }
        }

        writer.Append("<!DOCTYPE ");
        writer.Append(node.Name);
        if (publicId.Length != 0)
        {
            writer.Append(" PUBLIC ");
            WriteId(publicId, writer);
        }
        else if (systemId.Length != 0)
        {
            writer.Append(" SYSTEM");
        }

        if (systemId.Length != 0)
        {
            writer.Append(' ');
            WriteId(systemId, writer);
        }

        writer.Append('>');
    }

    private static void WriteId(string value, SerializationWriter writer)
    {
        var quote = '"';
        foreach (var character in value)
        {
            writer.Work.Charge(1, SerializationStage.Scan);
            if (character == '"') quote = '\'';
        }

        writer.Append(quote);
        writer.Append(value);
        writer.Append(quote);
    }

    private static void WriteEscaped(string value, SerializationWriter writer, bool requireWellFormed, bool attribute)
    {
        var runStart = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            writer.Work.Charge(1, SerializationStage.Scan);
            if (requireWellFormed && !IsXmlCharAt(value, ref index, writer.Work))
            {
                Invalid("The XML value contains an invalid character.");
            }

            string? replacement = character switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' when attribute => "&quot;",
                '\t' when attribute => "&#9;",
                '\n' when attribute => "&#xA;",
                '\r' when attribute => "&#xD;",
                _ => null
            };
            if (replacement is null) continue;
            writer.Append(value.AsSpan(runStart, index - runStart));
            writer.Append(replacement);
            runStart = index + 1;
        }

        writer.Append(value.AsSpan(runStart));
    }

    private static void ValidateXmlCharacters(string value, SerializationWork work)
    {
        for (var index = 0; index < value.Length; index++)
        {
            work.Charge(1, SerializationStage.Scan);
            if (!IsXmlCharAt(value, ref index, work)) Invalid("The XML value contains an invalid character.");
        }
    }

    // XML 1.0 fifth edition Char. A UTF-16 pair represents one astral scalar.
    private static bool IsXmlCharAt(string value, ref int index, SerializationWork work)
    {
        var character = value[index];
        if (char.IsHighSurrogate(character))
        {
            if (index + 1 == value.Length || !char.IsLowSurrogate(value[index + 1])) return false;
            index++;
            work.Charge(1, SerializationStage.Scan);
            return true;
        }

        if (char.IsLowSurrogate(character)) return false;
        return character is '\t' or '\n' or '\r' or >= ' ' and <= '\uD7FF' or >= '\uE000' and <= '\uFFFD';
    }

    private static bool IsPubidChar(char character)
        => character is ' ' or '\r' or '\n' or >= 'a' and <= 'z' or >= 'A' and <= 'Z' or
            >= '0' and <= '9' or '-' or '\'' or '(' or ')' or '+' or ',' or '.' or '/' or ':' or '=' or
            '?' or ';' or '!' or '*' or '#' or '@' or '$' or '_' or '%';

    private static bool AsciiEqualIgnoreCase(char actual, char expected)
        => actual == expected || actual == expected - 32;

    private static void Invalid(string message) => throw new DomException("InvalidStateError", message);
}
