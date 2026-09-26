using Jint.HtmlParser.Css.Values.Math;
using Jint.HtmlParser.Css.Values.References;

namespace Jint.HtmlParser.Css.Values.Properties;

internal enum CssPropertyStatus { Uninitialized, Valid, Deferred, Invalid, UnsupportedProperty, UnimplementedGrammar }
internal enum CssPropertyValueKind { Keyword, Numeric, Math, OverflowPair, Deferred, Custom }

internal sealed class CssPropertyValue
{
    private readonly CssNumericAtom _numeric;
    private readonly CssMathValue? _math;
    private readonly CssReferenceProgram? _references;
    private CssPropertyValue(CssPropertyValueKind kind, string text, CssSourceSpan span,
        CssNumericAtom numeric = default, CssMathValue? math = null, CssReferenceProgram? references = null,
        string? second = null)
    {
        Kind = kind; Text = text; Span = span; _numeric = numeric; _math = math;
        _references = references; SecondKeyword = second;
    }
    internal CssPropertyValueKind Kind { get; }
    internal string Text { get; }
    internal CssSourceSpan Span { get; }
    internal string? SecondKeyword { get; }
    internal CssNumericAtom Numeric => Kind == CssPropertyValueKind.Numeric ? _numeric : throw new InvalidOperationException();
    internal CssMathValue Math => Kind == CssPropertyValueKind.Math ? _math! : throw new InvalidOperationException();
    internal CssReferenceProgram References => Kind is CssPropertyValueKind.Deferred or CssPropertyValueKind.Custom
        ? _references! : throw new InvalidOperationException();
    internal static CssPropertyValue Keyword(string text, CssSourceSpan span) => new(CssPropertyValueKind.Keyword, text, span);
    internal static CssPropertyValue Number(CssNumericAtom atom, string text) => new(CssPropertyValueKind.Numeric, text, atom.Span, atom);
    internal static CssPropertyValue Calculation(CssMathValue math, string text) => new(CssPropertyValueKind.Math, text, math.Span, math: math);
    internal static CssPropertyValue Pair(string first, string second, CssSourceSpan span) =>
        new(CssPropertyValueKind.OverflowPair, first, span, second: second);
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
