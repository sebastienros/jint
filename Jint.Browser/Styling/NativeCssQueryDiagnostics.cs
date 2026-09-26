using Jint.HtmlParser;
using Jint.HtmlParser.Css.Model;

namespace Jint.Browser.Styling;

// Explicit query-local measurements. No callbacks, ambient collector or production observers.
internal sealed class NativeCssQueryDiagnostics(bool captureDetails = false)
{
    private readonly List<QueryRecord> _queries = [];
    internal IReadOnlyList<QueryRecord> Queries => _queries;

    internal QueryRecord QueryStarted()
    {
        var record = new QueryRecord(captureDetails);
        _queries.Add(record);
        return record;
    }

    internal sealed class QueryRecord
    {
        internal QueryRecord(bool details)
        {
            if (details)
            {
                Elements = new(ReferenceEqualityComparer.Instance);
                Rules = new(ReferenceEqualityComparer.Instance);
            }
        }

        internal long StatePublications { get; private set; }
        internal long RuleAttempts { get; private set; }
        internal long RuleMatches { get; private set; }
        internal Dictionary<string, long> ComputedPublications { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, long> CacheHits { get; } = new(StringComparer.Ordinal);
        internal Dictionary<Element, ElementRecord>? Elements { get; }
        internal Dictionary<CssStyleRule, RuleRecord>? Rules { get; }

        internal void StatePublished(Element element)
        {
            StatePublications++;
            if (ElementOf(element) is { } detail) detail.StatePublications++;
        }

        internal void RuleAttempted(Element element, CssStyleRule rule)
        {
            RuleAttempts++;
            if (ElementOf(element) is { } detail) detail.RuleAttempts++;
            if (RuleOf(rule) is { } perRule) perRule.Attempts++;
        }

        internal void RuleMatched(Element element, CssStyleRule rule)
        {
            RuleMatches++;
            if (ElementOf(element) is { } detail) detail.RuleMatches++;
            if (RuleOf(rule) is { } perRule) perRule.Matches++;
        }

        internal void ComputedPublished(Element element, string name)
        {
            Increment(ComputedPublications, name);
            if (ElementOf(element) is { } detail) Increment(detail.ComputedPublications, name);
        }

        internal void CacheHit(Element element, string name)
        {
            Increment(CacheHits, name);
            if (ElementOf(element) is { } detail) Increment(detail.CacheHits, name);
        }

        private ElementRecord? ElementOf(Element element)
        {
            if (Elements is null) return null;
            if (!Elements.TryGetValue(element, out var record)) Elements.Add(element, record = new());
            return record;
        }

        private RuleRecord? RuleOf(CssStyleRule rule)
        {
            if (Rules is null) return null;
            if (!Rules.TryGetValue(rule, out var record)) Rules.Add(rule, record = new());
            return record;
        }

        private static void Increment(Dictionary<string, long> counters, string name) =>
            counters[name] = counters.GetValueOrDefault(name) + 1;
    }

    internal sealed class ElementRecord
    {
        internal long StatePublications;
        internal long RuleAttempts;
        internal long RuleMatches;
        internal Dictionary<string, long> ComputedPublications { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, long> CacheHits { get; } = new(StringComparer.Ordinal);
    }

    internal sealed class RuleRecord
    {
        internal long Attempts;
        internal long Matches;
    }
}
