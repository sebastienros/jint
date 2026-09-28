using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Values;

// https://drafts.csswg.org/css-values-4/#urls
internal sealed record CssUrlValue(string Url, bool UsesSrc)
{
    internal string Serialize(CssValueWork work) =>
        (UsesSrc ? "src(" : "url(") + CssSyntaxSerializer.SerializeString(Url, work) + ")";

    internal static bool IsUrl(CssComponentValue component) => component.Kind switch
    {
        CssComponentKind.Token => component.Token.Kind == CssTokenKind.Url,
        CssComponentKind.Function => CssAscii.EqualsIgnoreCase(component.FunctionName, "url") ||
            CssAscii.EqualsIgnoreCase(component.FunctionName, "src"),
        _ => false
    };

    internal static CssPropertyResult Parse(CssComponentValue component, string modifierBlocker, CssValueWork work)
    {
        work.Charge(1);
        switch (component.Kind)
        {
            case CssComponentKind.Token when component.Token.Kind == CssTokenKind.Url:
                return CssPropertyResult.Accepted(CssPropertyValue.UrlValue(new(component.Token.Text, false), component.Span, work));
            case CssComponentKind.Function when IsUrl(component):
                var arguments = CssPropertyParser.Significant(component.Values, work);
                if (arguments.Count == 0 || arguments[0].Kind != CssComponentKind.Token ||
                    arguments[0].Token.Kind != CssTokenKind.String) break;
                if (arguments.Count > 1)
                {
                    for (var i = 1; i < arguments.Count; i++)
                    {
                        work.Charge(1);
                        var modifier = arguments[i];
                        if (modifier.Kind != CssComponentKind.Function &&
                            (modifier.Kind != CssComponentKind.Token || modifier.Token.Kind != CssTokenKind.Ident))
                            return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
                    }
                    return CssPropertyResult.Rejected(CssPropertyStatus.UnimplementedGrammar, modifierBlocker);
                }
                return CssPropertyResult.Accepted(CssPropertyValue.UrlValue(new(arguments[0].Token.Text,
                    CssAscii.EqualsIgnoreCase(component.FunctionName, "src")), component.Span, work));
        }
        return CssPropertyResult.Rejected(CssPropertyStatus.Invalid);
    }
}
