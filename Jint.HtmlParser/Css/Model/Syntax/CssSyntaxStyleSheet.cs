using Jint.HtmlParser.Css.Serialization;
using Jint.HtmlParser.Css.Syntax;

namespace Jint.HtmlParser.Css.Model.Syntax;

// CSS Syntax Level 3, §5.4.3: https://drafts.csswg.org/css-syntax/#parse-a-stylesheet
internal sealed class CssSyntaxStyleSheet
{
    private readonly List<CssSyntaxRule> _rules = new();
    private readonly CssSyntaxListView<CssSyntaxRule> _view;
    private ulong _version;

    private CssSyntaxStyleSheet() => _view = new CssSyntaxListView<CssSyntaxRule>(_rules);

    internal static CssSyntaxStyleSheet Parse(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var parsed = new CssSyntaxParser(source, options, cancellationToken).ParseStyleSheet();
        return FromSyntax(parsed, cancellationToken);
    }

    internal static CssSyntaxStyleSheet FromSyntax(CssRuleSyntax[] parsed, CancellationToken cancellationToken,
        Action<int>? onProjectionBatch = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sheet = new CssSyntaxStyleSheet();
        sheet._rules.Capacity = parsed.Length;
        for (var index = 0; index < parsed.Length; index++)
        {
            if ((index & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                onProjectionBatch?.Invoke(index);
            }
            sheet._rules.Add(new CssSyntaxRule(parsed[index], sheet));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return sheet;
    }

    internal IReadOnlyList<CssSyntaxRule> Rules => _view;
    internal CssMutationStamp Stamp => new(_version);

    internal int InsertRule(string source, int index, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if ((uint) index > (uint) _rules.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var syntax = new CssSyntaxParser(source, options, cancellationToken).ParseRule();
        cancellationToken.ThrowIfCancellationRequested();
        _rules.Insert(index, new CssSyntaxRule(syntax, this));
        CssMutationStamp.Advance(ref _version);
        return index;
    }

    internal void DeleteRule(int index)
    {
        if ((uint) index >= (uint) _rules.Count) throw new ArgumentOutOfRangeException(nameof(index));
        var removed = _rules[index];
        _rules.RemoveAt(index);
        removed.Detach();
        CssMutationStamp.Advance(ref _version);
    }

    internal string Serialize() => CssSyntaxSerializer.SerializeStyleSheet(_view);

    internal void RuleChanged() => CssMutationStamp.Advance(ref _version);
}
