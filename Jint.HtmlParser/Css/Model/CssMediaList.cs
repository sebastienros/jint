using System.Text;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;

namespace Jint.HtmlParser.Css.Model;

// CSSOM §4.4. One live collection; each text edit completes privately before commit.
internal sealed class CssMediaList
{
    private CssMediaQuery[] _queries;
    private CssRule? _rule;
    private CssStyleSheet? _sheet;
    private ulong _version;

    private CssMediaList(CssMediaQuery[] queries) => _queries = queries;
    internal int Count => _queries.Length;
    internal string this[int index] => _queries[index].Text;
    internal CssMutationStamp Stamp => new(_version);
    internal string MediaText => Serialize(new CssValueWork(default));

    internal string Serialize(CssValueWork work)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < _queries.Length; i++)
        {
            work.Charge(_queries[i].Text.Length);
            if (i != 0) builder.Append(", ");
            builder.Append(_queries[i].Text);
        }
        work.CheckCancellation();
        var text = builder.ToString();
        work.CheckCancellation();
        return text;
    }

    internal static CssMediaList Parse(string source, CssParseOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var parser = new CssSyntaxParser(source, options, cancellationToken);
        var values = parser.ParseComponentValues();
        return FromComponents(source, values, parser, new CssValueWork(cancellationToken));
    }

    internal static CssMediaList FromComponents(string source, CssComponentValueList values, CssSyntaxParser parser, CssValueWork work) =>
        new(CssMediaParser.Parse(values, source, parser, work));

    internal void AttachTo(CssRule rule) => _rule = rule;
    internal void AttachTo(CssStyleSheet sheet) => _sheet = sheet;

    internal bool Matches(CssMediaEnvironment environment, CancellationToken cancellationToken = default) =>
        Matches(environment, new CssValueWork(cancellationToken));

    internal bool Matches(CssMediaEnvironment environment, CssValueWork work)
    {
        work.CheckCancellation();
        if (_queries.Length == 0) return true;
        foreach (var query in _queries)
        {
            work.Charge(1);
            if (query.Matches(environment, work)) return true;
        }
        work.CheckCancellation();
        return false;
    }

    internal void SetMediaText(string source, CssParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        var replacement = Parse(source, options, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        _queries = replacement._queries;
        Changed();
    }

    internal void AppendMedium(string source, CssParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        var replacement = Parse(source, options, cancellationToken);
        if (replacement.Count != 1) return;
        var query = replacement._queries[0];
        var work = new CssValueWork(cancellationToken);
        foreach (var current in _queries)
        {
            work.Charge(current.Text.Length);
            work.Charge(query.Text.Length);
            if (current.Text == query.Text) { work.CheckCancellation(); return; }
        }
        var next = new CssMediaQuery[_queries.Length + 1];
        for (var i = 0; i < _queries.Length; i++) { work.Charge(1); next[i] = _queries[i]; }
        next[^1] = query;
        work.CheckCancellation();
        _queries = next;
        Changed();
    }

    internal void DeleteMedium(string source, CssParseOptions? options = null, CancellationToken cancellationToken = default)
    {
        var replacement = Parse(source, options, cancellationToken);
        if (replacement.Count != 1) return;
        var text = replacement[0];
        var work = new CssValueWork(cancellationToken);
        var next = new List<CssMediaQuery>();
        foreach (var current in _queries)
        {
            work.Charge(current.Text.Length);
            work.Charge(text.Length);
            if (current.Text != text) next.Add(current);
        }
        work.CheckCancellation();
        if (next.Count == _queries.Length) throw new DomException("NotFoundError", "The media query is not in the list.");
        var queries = next.ToArray();
        work.CheckCancellation();
        _queries = queries;
        Changed();
    }

    private void Changed()
    {
        CssMutationStamp.Advance(ref _version);
        _rule?.Changed();
        _sheet?.Changed();
    }
}
