using System;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Runtime.InteropServices;

namespace Jint.HtmlParser.Html;

internal enum HtmlReadStatus { Token, NeedInput, Yielded, Complete, InsertionBoundary }
internal enum HtmlTokenKind { Text, StartTag, EndTag, Comment, Doctype, ProcessingInstruction, EndOfFile }

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

internal readonly struct HtmlToken
{
    internal HtmlToken(HtmlTokenKind kind, string data = "", string? name = null,
        IReadOnlyList<HtmlAttribute>? attributes = null, bool selfClosing = false,
        string? publicIdentifier = null, string? systemIdentifier = null,
        bool forceQuirks = false, long offset = 0, bool endTagHadAttributes = false,
        bool endTagHadSelfClosing = false, HtmlSourceLocation? scriptSourceLocation = null,
        long sourceChanges = 0, StringSlice dataSlice = default)
    {
        Kind = kind;
        ScriptSourceLocation = scriptSourceLocation;
        SourceChanges = sourceChanges;
        DataSlice = dataSlice.IsEmpty ? new StringSlice(data) : dataSlice;
        Name = name;
        Attributes = attributes ?? Array.Empty<HtmlAttribute>();
        SelfClosing = selfClosing;
        PublicIdentifier = publicIdentifier;
        SystemIdentifier = systemIdentifier;
        ForceQuirks = forceQuirks;
        Offset = offset;
        EndTagHadAttributes = endTagHadAttributes;
        EndTagHadSelfClosing = endTagHadSelfClosing;
    }

    internal HtmlSourceLocation? ScriptSourceLocation { get; }
    internal long SourceChanges { get; }
    internal HtmlTokenKind Kind { get; }
    internal StringSlice DataSlice { get; }
    internal string Data => DataSlice.ToString();
    internal string? Name { get; }
    internal IReadOnlyList<HtmlAttribute> Attributes { get; }
    internal bool SelfClosing { get; }
    internal string? PublicIdentifier { get; }
    internal string? SystemIdentifier { get; }
    internal bool ForceQuirks { get; }
    internal long Offset { get; }
    internal bool EndTagHadAttributes { get; }
    internal bool EndTagHadSelfClosing { get; }
}
