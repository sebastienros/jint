using System;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace Jint.HtmlParser.Html;

internal enum HtmlReadStatus { Token, NeedInput, Yielded, Complete, InsertionBoundary }
internal enum HtmlTokenKind : byte { Text, StartTag, EndTag, Comment, Doctype, ProcessingInstruction, EndOfFile }

internal readonly record struct HtmlAttribute(string Name, StringSlice ValueSlice)
{
    internal HtmlAttribute(string name, string value) : this(name, new StringSlice(value)) { }
    internal string Value => ValueSlice.ToString();
    public bool Equals(HtmlAttribute other) => Name == other.Name && ValueSlice.Span.SequenceEqual(other.ValueSlice.Span);
    public override int GetHashCode() => HashCode.Combine(Name, XxHash3.HashToUInt64(MemoryMarshal.AsBytes(ValueSlice.Span)));
}

internal readonly struct HtmlTokenizerContext
{
    private readonly ParseLimits? _limits;

    internal HtmlTokenizerContext(ParseLimits? limits = null,
        ParseDiagnosticCollector? diagnostics = null, bool allowCData = false)
    {
        _limits = limits;
        Diagnostics = diagnostics;
        AllowCData = allowCData;
    }

    internal ParseLimits Limits => _limits ?? ParseLimits.Unbounded;
    internal ParseDiagnosticCollector? Diagnostics { get; }
    internal bool AllowCData { get; }
}

// Copied into tree-builder state for every token, so rarely used members live in one
// side reference: DOCTYPE identifiers, or the source location of a script start tag.
internal readonly struct HtmlToken
{
    private readonly object? _extra;
    private readonly HtmlAttribute[] _attributes;
    private readonly TokenFlags _flags;

    internal HtmlToken(HtmlTokenKind kind, string data = "", string? name = null,
        HtmlAttribute[]? attributes = null, bool selfClosing = false,
        string? publicIdentifier = null, string? systemIdentifier = null,
        bool forceQuirks = false, long offset = 0, bool endTagHadAttributes = false,
        bool endTagHadSelfClosing = false, HtmlSourceLocation? scriptSourceLocation = null,
        long sourceChanges = 0, StringSlice dataSlice = default)
    {
        Kind = kind;
        SourceChanges = sourceChanges;
        DataSlice = dataSlice.IsEmpty ? new StringSlice(data) : dataSlice;
        Name = name;
        _attributes = attributes ?? [];
        Offset = offset;
        if (publicIdentifier is not null || systemIdentifier is not null)
            _extra = new DoctypeIdentifiers(publicIdentifier, systemIdentifier);
        else if (scriptSourceLocation is { } location)
            _extra = location;
        _flags = (selfClosing ? TokenFlags.SelfClosing : 0) |
                 (forceQuirks ? TokenFlags.ForceQuirks : 0) |
                 (endTagHadAttributes ? TokenFlags.EndTagHadAttributes : 0) |
                 (endTagHadSelfClosing ? TokenFlags.EndTagHadSelfClosing : 0);
    }

    internal HtmlSourceLocation? ScriptSourceLocation => _extra as HtmlSourceLocation?;
    internal long SourceChanges { get; }
    internal HtmlTokenKind Kind { get; }
    internal StringSlice DataSlice { get; }
    internal string Data => DataSlice.ToString();
    internal string? Name { get; }
    internal HtmlAttribute[] Attributes => _attributes;
    internal bool SelfClosing => (_flags & TokenFlags.SelfClosing) != 0;
    internal string? PublicIdentifier => (_extra as DoctypeIdentifiers)?.Public;
    internal string? SystemIdentifier => (_extra as DoctypeIdentifiers)?.System;
    internal bool ForceQuirks => (_flags & TokenFlags.ForceQuirks) != 0;
    internal long Offset { get; }
    internal bool EndTagHadAttributes => (_flags & TokenFlags.EndTagHadAttributes) != 0;
    internal bool EndTagHadSelfClosing => (_flags & TokenFlags.EndTagHadSelfClosing) != 0;

    [Flags]
    private enum TokenFlags : byte
    {
        SelfClosing = 1,
        ForceQuirks = 2,
        EndTagHadAttributes = 4,
        EndTagHadSelfClosing = 8,
    }

    private sealed record DoctypeIdentifiers(string? Public, string? System);
}
