using System.Collections;
using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Selectors;

namespace Jint.HtmlParser.Css.Model;

internal enum CssRuleType { Style }

// CSSOM §6.4: exposed parent links and attachment ownership are deliberately separate.
internal abstract class CssRule
{
    private ulong _version;
    private CssRule? _attachmentParent;
    private CssStyleSheet? _attachmentSheet;

    protected CssRule(CssSourceSpan sourceSpan) => SourceSpan = sourceSpan;

    internal abstract CssRuleType Type { get; }
    internal CssSourceSpan SourceSpan { get; }
    internal CssRule? ParentRule { get; private set; }
    internal CssStyleSheet? ParentStyleSheet { get; private set; }
    internal CssMutationStamp Stamp => new(_version);
    internal string CssText => CssRuleSerializer.Serialize(this);

    internal void Attach(CssStyleSheet sheet, CssRule? parent)
    {
        ParentStyleSheet = sheet;
        ParentRule = parent;
        _attachmentSheet = parent is null ? sheet : null;
        _attachmentParent = parent;
    }

    internal void Detach()
    {
        ParentStyleSheet = null;
        ParentRule = null;
        _attachmentSheet = null;
        _attachmentParent = null;
    }

    internal void Changed()
    {
        CssRule? current = this;
        while (current is not null)
        {
            CssMutationStamp.Advance(ref current._version);
            current._attachmentSheet?.Changed();
            current = current._attachmentParent;
        }
    }
}

internal sealed class CssStyleRule : CssRule
{
    private CompiledSelector _selector;
    private string _selectorText;

    internal CssStyleRule(CompiledSelector selector, string selectorText, CssDeclarationBlock style, CssSourceSpan span)
        : base(span)
    {
        _selector = selector;
        _selectorText = selectorText;
        Style = style;
        style.AttachTo(this);
    }

    internal override CssRuleType Type => CssRuleType.Style;
    internal CssDeclarationBlock Style { get; }
    internal CompiledSelector Selector => _selector;
    // This stage retains validated author selector text; canonical selector serialization is separate.
    internal string SelectorText => _selectorText;

    internal bool TryMatch(Element element, out SelectorSpecificity specificity, Node? scopingRoot = null,
        CancellationToken cancellationToken = default) =>
        SelectorMatcher.TryMatch(_selector, element, out specificity, scopingRoot, cancellationToken);

    internal void SetSelectorText(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parser = new CssSyntaxParser(source, options, cancellationToken);
            var values = parser.ParseComponentValues();
            var selector = new SelectorCompiler.Worker(source,
                new SelectorParseContext(limits: options?.Limits), cancellationToken).Compile(values);
            var text = CssStyleSheet.SelectorText(source, values, new Values.CssValueWork(cancellationToken));
            cancellationToken.ThrowIfCancellationRequested();
            _selector = selector;
            _selectorText = text;
            Changed();
        }
        catch (SelectorParseException)
        {
            // CSSOM's selectorText setter preserves the old selector on selector syntax failure.
        }
    }
}

internal sealed class CssRuleList(List<CssRule> items) : IReadOnlyList<CssRule>
{
    public int Count => items.Count;
    public CssRule this[int index] => items[index];
    public IEnumerator<CssRule> GetEnumerator() => items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class CssIncompleteRuleGrammarException : NotSupportedException
{
    internal CssIncompleteRuleGrammarException(string ruleName, string blocker, CssSourceSpan span)
        : base("Unimplemented CSS rule grammar: " + blocker)
    { RuleName = ruleName; Blocker = blocker; Span = span; }

    internal string RuleName { get; }
    internal string Blocker { get; }
    internal CssSourceSpan Span { get; }
}
