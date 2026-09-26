namespace Jint.HtmlParser.Css.Model;

// CSSOM §6.4.5. Stable children and MediaList identities, including detached retained groups.
internal sealed class CssMediaRule : CssConditionRule
{
    internal CssMediaRule(CssMediaList media, CssSourceSpan span) : base(span)
    {
        Media = media;
        media.AttachTo(this);
    }

    internal override CssRuleType Type => CssRuleType.Media;
    internal CssMediaList Media { get; }
    internal override string ConditionText => Media.MediaText;
}
