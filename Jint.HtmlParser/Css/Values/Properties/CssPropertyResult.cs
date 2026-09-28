using Jint.HtmlParser.Css.Values.Colors;
using Jint.HtmlParser.Css.Values.Descriptors;
using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.References;
using Jint.HtmlParser.Css.Values.Transforms;
using Jint.HtmlParser.Css.Serialization;

namespace Jint.HtmlParser.Css.Values.Properties;

internal enum CssPropertyStatus { Uninitialized, Valid, Deferred, Invalid, UnsupportedProperty, UnimplementedGrammar }
internal enum CssPropertyValueKind { Keyword, Numeric, Math, OverflowPair, Shorthand, FitContent, Deferred, Custom, Color, Transform, TransformList, KeywordList, Descriptor, IdentifierList, PaintServer }

internal sealed class CssPropertyValue
{
    private readonly CssFontFaceDescriptorValue? _descriptor;
    private readonly CssColorValue? _color;
    private readonly IReadOnlyList<string>? _identifiers;
    private readonly CssTransformValue? _transform;
    private readonly CssTransformList? _transformList;
    private readonly CssNumericAtom _numeric;
    private readonly CssMathValue? _math;
    private readonly CssReferenceProgram? _references;
    private readonly IReadOnlyList<CssPropertyValue>? _components;
    private readonly string? _paintUrl;
    private readonly CssPropertyValue? _paintFallback;
    private CssPropertyValue(CssPropertyValueKind kind, string text, CssSourceSpan span,
        CssNumericAtom numeric = default, CssMathValue? math = null, CssReferenceProgram? references = null,
        CssColorValue? color = null, string? second = null, IReadOnlyList<CssPropertyValue>? components = null,
        CssTransformValue? transform = null, CssTransformList? transformList = null, CssFontFaceDescriptorValue? descriptor = null,
        IReadOnlyList<string>? identifiers = null, string? paintUrl = null, CssPropertyValue? paintFallback = null,
        bool paintUsesSrc = false)
    {
        Kind = kind; Text = text; Span = span; _numeric = numeric; _math = math;
        _color = color; _references = references; SecondKeyword = second; _components = components;
        _transform = transform; _transformList = transformList; _descriptor = descriptor; _identifiers = identifiers;
        _paintUrl = paintUrl; _paintFallback = paintFallback;
        PaintUsesSrc = paintUsesSrc;
    }
    internal CssFontFaceDescriptorValue DescriptorValue => Kind == CssPropertyValueKind.Descriptor ? _descriptor! : throw new InvalidOperationException();
    internal static CssPropertyValue Descriptor(CssFontFaceDescriptorValue value) => new(CssPropertyValueKind.Descriptor, value.Text, default, descriptor: value);
    internal IReadOnlyList<string> Identifiers => Kind == CssPropertyValueKind.IdentifierList
        ? _identifiers! : throw new InvalidOperationException();
    internal static CssPropertyValue IdentifierList(string text, CssSourceSpan span, string[] identifiers) =>
        new(CssPropertyValueKind.IdentifierList, text, span, identifiers: Array.AsReadOnly(identifiers));
    internal CssPropertyValueKind Kind { get; }
    internal string PaintUrl => Kind == CssPropertyValueKind.PaintServer ? _paintUrl! : throw new InvalidOperationException();
    internal CssPropertyValue? PaintFallback => Kind == CssPropertyValueKind.PaintServer ? _paintFallback : throw new InvalidOperationException();
    internal bool PaintUsesSrc { get; }
    internal static CssPropertyValue PaintServer(string url, CssPropertyValue? fallback, CssSourceSpan span, CssValueWork work,
        bool usesSrc = false)
    {
        if (fallback is not null && fallback.Kind != CssPropertyValueKind.Color &&
            fallback is not { Kind: CssPropertyValueKind.Keyword, Text: "none" })
            throw new ArgumentException("A paint fallback must be a color or none.", nameof(fallback));
        var text = (usesSrc ? "src(" : "url(") + CssSyntaxSerializer.SerializeString(url, work) + ")";
        if (fallback is not null) text += " " + fallback.Serialize();
        work.Charge(text.Length);
        return new(CssPropertyValueKind.PaintServer, text, span, paintUrl: url, paintFallback: fallback, paintUsesSrc: usesSrc);
    }
    internal string Text { get; }
    internal CssSourceSpan Span { get; }
    internal string? SecondKeyword { get; }
    internal IReadOnlyList<CssPropertyValue> Components => Kind is CssPropertyValueKind.Shorthand or CssPropertyValueKind.FitContent or CssPropertyValueKind.KeywordList
        ? _components! : throw new InvalidOperationException();
    internal CssColorValue Color => Kind == CssPropertyValueKind.Color ? _color! : throw new InvalidOperationException();
    internal CssTransformValue Transform => Kind == CssPropertyValueKind.Transform ? _transform! : throw new InvalidOperationException();
    internal static CssPropertyValue TransformValue(CssTransformValue transform, string text) =>
        new(CssPropertyValueKind.Transform, text, transform.Span, transform: transform);
    internal CssTransformList TransformList => Kind == CssPropertyValueKind.TransformList ? _transformList! : throw new InvalidOperationException();
    internal static CssPropertyValue TransformListValue(CssTransformList list, string text) =>
        new(CssPropertyValueKind.TransformList, text, list.Span, transformList: list);
    internal CssNumericAtom Numeric => Kind == CssPropertyValueKind.Numeric ? _numeric : throw new InvalidOperationException();
    internal CssMathValue Math => Kind == CssPropertyValueKind.Math ? _math! : throw new InvalidOperationException();
    internal CssReferenceProgram References => Kind is CssPropertyValueKind.Deferred or CssPropertyValueKind.Custom
        ? _references! : throw new InvalidOperationException();
    internal static CssPropertyValue ColorValue(CssColorValue color, string text) => new(CssPropertyValueKind.Color, text, color.Span, color: color);
    internal static CssPropertyValue Keyword(string text, CssSourceSpan span) => new(CssPropertyValueKind.Keyword, text, span);
    internal static CssPropertyValue KeywordList(string text, CssSourceSpan span,
        IReadOnlyList<CssPropertyValue> values, CssValueWork work)
    {
        var owned = new CssPropertyValue[values.Count];
        for (var i = 0; i < owned.Length; i++)
        {
            work.Charge(1);
            var value = values[i];
            if (value.Kind != CssPropertyValueKind.Keyword) throw new ArgumentException("Keyword layers are required.", nameof(values));
            owned[i] = value;
        }
        work.CheckCancellation();
        return new(CssPropertyValueKind.KeywordList, text, span, components: Array.AsReadOnly(owned));
    }
    internal static CssPropertyValue Number(CssNumericAtom atom, string text) => new(CssPropertyValueKind.Numeric, text, atom.Span, atom);
    internal static CssPropertyValue Calculation(CssMathValue math, string text) => new(CssPropertyValueKind.Math, text, math.Span, math: math);
    internal static CssPropertyValue Pair(string first, string second, CssSourceSpan span) =>
        new(CssPropertyValueKind.OverflowPair, first, span, second: second);
    // Arrays are newly owned by the family parser and never exposed for mutation.
    internal static CssPropertyValue Shorthand(string text, CssSourceSpan span, params CssPropertyValue[] values) =>
        new(CssPropertyValueKind.Shorthand, text, span, components: Array.AsReadOnly(values));
    internal static CssPropertyValue FitContent(CssPropertyValue argument, CssSourceSpan span) =>
        new(CssPropertyValueKind.FitContent, "fit-content(" + argument.Serialize() + ")", span,
            components: Array.AsReadOnly(new[] { argument }));
    internal static CssPropertyValue Reference(CssReferenceProgram program, string text, bool custom) =>
        new(custom ? CssPropertyValueKind.Custom : CssPropertyValueKind.Deferred, text, default, references: program);
    internal string Serialize() => Kind == CssPropertyValueKind.OverflowPair && Text != SecondKeyword
        ? Text + " " + SecondKeyword : Text;
}

internal readonly struct CssPropertyResult
{
    private readonly CssPropertyValue? _value;
    private CssPropertyResult(CssPropertyStatus status, CssPropertyValue? value, string? blocker)
    { Status = status; _value = value; Blocker = blocker; }
    internal CssPropertyStatus Status { get; }
    internal string? Blocker { get; }
    internal CssPropertyValue Value => Status is CssPropertyStatus.Valid or CssPropertyStatus.Deferred
        ? _value! : throw new InvalidOperationException("No validated property value.");
    internal static CssPropertyResult Accepted(CssPropertyValue value, bool deferred = false) =>
        new(deferred ? CssPropertyStatus.Deferred : CssPropertyStatus.Valid, value, null);
    internal static CssPropertyResult Rejected(CssPropertyStatus status, string? blocker = null) => new(status, null, blocker);
}
