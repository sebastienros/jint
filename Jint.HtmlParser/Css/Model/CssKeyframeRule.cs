using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Model;

// CSS Animations 1 §6.2. Invalid keyText throws without publishing any mutation.
// https://drafts.csswg.org/css-animations-1/#interface-csskeyframerule
internal sealed class CssKeyframeRule : CssRule
{
    private CssKeyframeKeys _keys;

    internal CssKeyframeRule(CssKeyframeKeys keys, CssDeclarationBlock style, CssSourceSpan span) : base(span)
    {
        _keys = keys;
        Style = style;
        style.AttachTo(this);
    }

    internal override CssRuleType Type => CssRuleType.Keyframe;
    internal CssDeclarationBlock Style { get; }
    internal string KeyText => _keys.Text;
    internal bool Matches(CssKeyframeKeys keys, CssValueWork work) => _keys.Matches(keys, work);

    internal void SetKeyText(string text, CssParseOptions? options = null, CssValueWork? work = null)
    {
        work ??= new CssValueWork(default);
        var keys = CssKeyframeKeys.Parse(text, options, work);
        if (keys is null) throw new DomException("SyntaxError", "Invalid keyframe selector.");
        work.CheckCancellation();
        _keys = keys;
        Changed();
    }
}
