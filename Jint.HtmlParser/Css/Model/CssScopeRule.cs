using Jint.HtmlParser.Css.Selectors;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;
using Jint.HtmlParser.Css.Values.Properties;

namespace Jint.HtmlParser.Css.Model;

// CSS Cascade 6 §3.5 https://drafts.csswg.org/css-cascade-6/#scoped-styles
// <scope-start> is compiled in the enclosing context: a nested @scope's start is itself scoped by the
// outer rule. <scope-end> and the scoped style rules are relative to this rule's own scoping root, so
// they compile against Anchor, an unattached `:where(:scope)` nesting parent (§3.5.1: & behaves as
// :where(:scope); §3.5.3: a selector without :scope or & is relative to :scope by a descendant).
internal sealed class CssScopeRule : CssGroupingRule
{
    private CssScopeRule(string? start, CompiledSelector? startSelector, string? end, CompiledSelector? endSelector,
        CssStyleRule anchor, CssSourceSpan span) : base(span)
    {
        Start = start;
        StartSelector = startSelector;
        End = end;
        EndSelector = endSelector;
        Anchor = anchor;
    }

    internal override CssRuleType Type => CssRuleType.Scope;
    internal string? Start { get; }
    internal string? End { get; }
    internal CompiledSelector? StartSelector { get; }
    internal CompiledSelector? EndSelector { get; }

    // The nesting parent every scoped style rule, and this rule's <scope-end>, compiles against.
    internal CssStyleRule Anchor { get; }

    // §3.5.2: @scope [(<scope-start>)]? [to (<scope-end>)]?. Pseudo-elements are invalid in both.
    internal static CssScopeRule? Parse(string source, CssRuleSyntax syntax, CssSyntaxParser parser,
        CssParseOptions? options, CssValueWork work, CssStyleRule? context, CancellationToken cancellationToken)
    {
        if (syntax.Block is null) return null;
        var prelude = syntax.Prelude;
        var index = 0;
        Skip();
        CssComponentValue? startBlock = null, endBlock = null;
        if (index < prelude.Count && IsParenthesized(prelude[index]))
        {
            startBlock = prelude[index++];
            Skip();
        }
        if (index < prelude.Count)
        {
            var keyword = prelude[index++];
            if (keyword.Kind != CssComponentKind.Token || keyword.Token.Kind != CssTokenKind.Ident ||
                !CssAscii.EqualsIgnoreCase(keyword.Token.Text, "to"))
                return null;
            Skip();
            if (index == prelude.Count || !IsParenthesized(prelude[index])) return null;
            endBlock = prelude[index++];
            Skip();
            if (index != prelude.Count) return null;
        }
        var limits = options?.Limits;
        string? start = null, end = null;
        CompiledSelector? startSelector = null, endSelector = null;
        if (startBlock is { } startValue)
        {
            startSelector = Compile(source, startValue.Values, limits, work, context, cancellationToken);
            if (startSelector is null) return null;
            start = CssStyleSheet.SelectorText(source, startValue.Values, parser, work);
        }
        var anchor = CreateAnchor(syntax.Span, limits, work);
        if (endBlock is { } endValue)
        {
            endSelector = Compile(source, endValue.Values, limits, work, anchor, cancellationToken);
            if (endSelector is null) return null;
            end = CssStyleSheet.SelectorText(source, endValue.Values, parser, work);
        }
        work.CheckCancellation();
        return new CssScopeRule(start, startSelector, end, endSelector, anchor, syntax.Span);

        void Skip()
        {
            while (index < prelude.Count && prelude[index].Kind == CssComponentKind.Token &&
                prelude[index].Token.Kind == CssTokenKind.Whitespace)
            {
                work.Charge(1);
                index++;
            }
        }
    }

    // The innermost @scope anchor or style rule that a rule nested under `owner` compiles against.
    // Grouping rules other than @scope are transparent; a style rule is the nearest nesting parent.
    internal static CssStyleRule? NestingContextOf(CssRule? owner, CssValueWork? work = null)
    {
        for (var current = owner; current is not null; current = current.ParentRule)
        {
            work?.Charge(1);
            switch (current)
            {
                case CssStyleRule style: return style;
                case CssScopeRule scope: return scope.Anchor;
            }
        }
        return null;
    }

    // The nesting context a direct child of `owner` compiles against, given the context `owner`
    // itself was parsed in. This is NestingContextOf for rules whose parent links are not yet attached.
    internal static CssStyleRule? ChildContext(CssRule owner, CssStyleRule? ownerContext) => owner switch
    {
        CssStyleRule style => style,
        CssScopeRule scope => scope.Anchor,
        _ => ownerContext
    };

    private static bool IsParenthesized(CssComponentValue value) =>
        value.Kind == CssComponentKind.SimpleBlock && value.OpeningDelimiter == '(';

    private static CompiledSelector? Compile(string source, CssComponentValueList values, ParseLimits? limits,
        CssValueWork work, CssStyleRule? context, CancellationToken cancellationToken)
    {
        try
        {
            var selector = new SelectorCompiler.Worker(source,
                SelectorParseContext.For(limits, context),
                cancellationToken, work.CheckCancellation).Compile(values);
            return ContainsPseudoElement(selector, work) ? null : selector;
        }
        catch (SelectorParseException) { return null; }
    }

    private static bool ContainsPseudoElement(CompiledSelector selector, CssValueWork work)
    {
        foreach (var branch in selector.BranchArray)
            foreach (var compound in branch.CompoundArray)
                foreach (var predicate in compound.PredicateArray)
                {
                    work.Charge(1);
                    if (predicate.Kind is CompiledSelector.PredicateKind.PseudoElement
                        or CompiledSelector.PredicateKind.WebkitUnknownPseudoElement
                        or CompiledSelector.PredicateKind.Picker or CompiledSelector.PredicateKind.Slotted)
                        return true;
                }
        return false;
    }

    private const string AnchorText = ":where(:scope)";

    // The anchor program is constant and immutable; only the anchor rule identity is per @scope.
    private static readonly CompiledSelector AnchorSelector =
        SelectorCompiler.Compile(AnchorText, new SelectorParseContext(), CancellationToken.None);

    private static CssStyleRule CreateAnchor(CssSourceSpan span, ParseLimits? limits, CssValueWork work)
    {
        work.Charge(1);
        return new CssStyleRule(AnchorSelector, AnchorText,
            CssDeclarationBlock.FromDeclarations(string.Empty, [], CssDeclarationContext.Style, 0, work), span, limits,
            scopeAnchor: true);
    }
}
