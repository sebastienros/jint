namespace Jint.HtmlParser.Css.Model;

// CSS Fonts 4 §4.1 and §12.1. Usability for font selection is separate from CSSOM retention.
internal sealed class CssFontFaceRule : CssRule
{
    internal CssFontFaceRule(CssDeclarationBlock style, CssSourceSpan span) : base(span)
    {
        Style = style;
        style.AttachTo(this);
    }

    internal override CssRuleType Type => CssRuleType.FontFace;
    internal CssDeclarationBlock Style { get; }
}
