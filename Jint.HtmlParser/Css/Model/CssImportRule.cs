using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Model;

// https://drafts.csswg.org/cssom/#the-cssimportrule-interface
// https://drafts.csswg.org/css-cascade-5/#at-import
internal sealed class CssImportRule : CssRule
{
    private CssImportRule(string href, CssMediaList media, CssSourceSpan span) : base(span)
    {
        Href = href;
        Media = media;
        media.AttachTo(this);
    }

    internal override CssRuleType Type => CssRuleType.Import;
    internal string Href { get; }
    internal CssMediaList Media { get; }
    internal CssStyleSheet? StyleSheet { get; private set; }

    // Browser resolves/fetches privately, then publishes the actual child identity on its loop.
    // Never share one fetched sheet between two import occurrences.
    internal void SetStyleSheet(CssStyleSheet? sheet, Uri? sourceUrl, Uri? baseUrl, CssValueWork work)
    {
        work.CheckCancellation();
        if (ReferenceEquals(sheet, StyleSheet)) return;
        // A published child belongs to this historical rule for its lifetime. Browser must
        // replace the parent generation, never retarget a retained import/media wrapper.
        if (StyleSheet is not null)
            throw new InvalidOperationException("A published import child cannot be replaced or cleared.");
        if (sheet is not null)
        {
            if (sheet.Attachment.OwnerNode is not null || sheet.Attachment.ImportOwner is not null)
                throw new InvalidOperationException("The imported sheet already has an owner.");
            foreach (var descendant in sheet.ImportedStyleSheets(work))
            {
                work.Charge(1);
                if (ReferenceEquals(descendant, ParentStyleSheet))
                    throw new InvalidOperationException("An import cannot create a sheet cycle.");
            }
        }
        // Stage all potentially cancelling traversal before ownership changes.
        work.CheckCancellation();
        StyleSheet = sheet;
        sheet?.SetImportAttachment(this, Media, sourceUrl, baseUrl);
        Changed();
    }

    internal static CssImportRule? Parse(string source, CssRuleSyntax syntax, CssSyntaxParser parser, CssValueWork work)
    {
        if (syntax.Block is not null) return null;
        var values = syntax.Prelude;
        var index = 0;
        while (index < values.Count && White(values[index])) { work.Charge(1); index++; }
        if (index == values.Count) return null;
        var url = values[index++];
        string href;
        if (url.Kind == CssComponentKind.Token && url.Token.Kind is CssTokenKind.String or CssTokenKind.Url)
            href = url.Token.Text;
        else if (url.Kind == CssComponentKind.Function && CssAscii.EqualsIgnoreCase(url.FunctionName, "url"))
        {
            var arguments = url.Values;
            var first = 0;
            var last = arguments.Count - 1;
            while (first <= last && White(arguments[first])) { work.Charge(1); first++; }
            while (last >= first && White(arguments[last])) { work.Charge(1); last--; }
            if (first != last || arguments[first].Kind != CssComponentKind.Token || arguments[first].Token.Kind != CssTokenKind.String)
                return null;
            href = arguments[first].Token.Text;
        }
        else return null;
        while (index < values.Count && White(values[index])) { work.Charge(1); index++; }
        // Finite R1a: these must never be mistaken for an unconditional media list.
        if (index < values.Count)
        {
            var condition = values[index];
            if (condition.Kind == CssComponentKind.Token && condition.Token.Kind == CssTokenKind.Ident &&
                CssAscii.EqualsIgnoreCase(condition.Token.Text, "layer") ||
                condition.Kind == CssComponentKind.Function && CssAscii.EqualsIgnoreCase(condition.FunctionName, "layer"))
                throw new CssIncompleteRuleGrammarException("import", "R1:import-prelude-layer", syntax.Span);
            if (condition.Kind == CssComponentKind.Function && CssAscii.EqualsIgnoreCase(condition.FunctionName, "supports"))
                throw new CssIncompleteRuleGrammarException("import", "R1:import-prelude-supports", syntax.Span);
        }
        var remaining = new CssComponentValue[values.Count - index];
        for (var i = 0; i < remaining.Length; i++) { work.Charge(1); remaining[i] = values[index + i]; }
        return new CssImportRule(href, CssMediaList.FromComponents(source, new CssComponentValueList(remaining), parser, work), syntax.Span);
    }

    private static bool White(CssComponentValue value) =>
        value.Kind == CssComponentKind.Token && value.Token.Kind == CssTokenKind.Whitespace;
}
