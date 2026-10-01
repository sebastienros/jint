using System.Runtime.CompilerServices;
using Jint.HtmlParser.Css;
using Jint.HtmlParser.Css.Media;
using Jint.HtmlParser.Css.Model;
using Jint.HtmlParser.Css.Model.Syntax;
using Jint.HtmlParser.Css.Syntax;
using Jint.HtmlParser.Css.Values;

namespace Jint.Browser.Styling;

// Browser-owned source generations and parse-result caches. Native CSSOM getters never parse.
internal static class NativeCssParsing
{
    private static readonly ConditionalWeakTable<CssRuleList, RuleSlot> RuleSources = new();
    private static readonly ConditionalWeakTable<CssMediaList, MediaSlot> MediaSources = new();

    private sealed class RuleSlot
    {
        internal RuleInput? Input;
        internal bool Reading;
    }

    private sealed class RuleInput(CssStyleSheet sheet, CssRule? parent, CssMutationStamp stamp,
        CssRawRule[]? rules, CssRuleBody? body, int importEnd)
    {
        internal readonly CssStyleSheet Sheet = sheet;
        internal readonly CssRule? Parent = parent;
        internal CssMutationStamp Stamp = stamp;
        internal readonly CssRuleBody? Body = body;
        internal readonly int ImportEnd = importEnd;
        internal CssRawRule[]? Rules = rules;
        internal int Processed;
        internal bool ImportsAllowed = true;
        internal bool HasImport;
        internal bool ImportsRead;
    }

    private sealed class MediaSlot
    {
        internal MediaInput? Input;
        internal bool Reading;
    }

    private sealed class MediaInput(CssSourceText source, CssMutationStamp stamp)
    {
        internal CssSourceText Source { get; } = source;
        internal CssMutationStamp Stamp = stamp;
    }

    internal static CssStyleSheet CreateSheet(string source, CssValueWork work)
    {
        var rules = CssParser.ParseRuleList(source, work);
        var sheet = new CssStyleSheet();
        var input = SheetInput(sheet, rules, work);
        var slot = RuleSources.GetValue(sheet.Rules, static _ => new());
        work.CheckCancellation();
        slot.Input = input;
        return sheet;
    }

    internal static void ReplaceSource(CssStyleSheet sheet, string source, CssValueWork work)
    {
        var rules = CssParser.ParseRuleList(source, work);
        var slot = RuleSources.GetValue(sheet.Rules, static _ => new());
        var input = SheetInput(sheet, rules, work);
        sheet.Replace(new CssStyleSheet(), work);
        // Replace has committed. Source publication is callback-free and preserves the live list.
        input.Stamp = sheet.Rules.Stamp;
        slot.Input = input;
    }

    private static RuleInput SheetInput(CssStyleSheet sheet, CssRawRule[] rules, CssValueWork work)
    {
        var importEnd = 0;
        for (var i = 0; i < rules.Length; i++)
        {
            work.Charge(1);
            if (IsImport(rules[i])) importEnd = i + 1;
        }
        return new(sheet, null, sheet.Rules.Stamp, rules, null, importEnd);
    }

    internal static CssRuleList ReadRules(CssRuleList rules, CssValueWork work) => ReadRules(rules, work, importsOnly: false);
    internal static CssRuleList ImportRules(CssStyleSheet sheet, CssValueWork work) => ReadRules(sheet.Rules, work, importsOnly: true);

    private static CssRuleList ReadRules(CssRuleList rules, CssValueWork work, bool importsOnly)
    {
        if (!RuleSources.TryGetValue(rules, out var slot) || slot.Input is not { } input) return rules;
        if (slot.Reading) throw new InvalidOperationException("A CSS rule list cannot be parsed reentrantly.");
        var stamp = rules.Stamp;
        var guarded = CssValueWork.Guard(work, () =>
        {
            work.CheckCancellation();
            if (!ReferenceEquals(slot.Input, input) || !stamp.CanReuse || rules.Stamp != stamp || stamp != input.Stamp)
                throw new InvalidOperationException(NativeCssQuery.Invalidated);
        });
        slot.Reading = true;
        try
        {
            guarded.CheckCancellation();
            if (importsOnly && (input.ImportsRead || input.ImportEnd == 0)) return rules;
            var raw = input.Rules ?? CssParser.ParseRuleList(input.Body!.Value, guarded);
            var end = importsOnly ? input.ImportEnd : raw.Length;
            var result = new List<CssRule>(rules.Count);
            foreach (var rule in rules) { guarded.Charge(1); result.Add(rule); }
            var allowed = input.ImportsAllowed;
            var hasImport = input.HasImport;
            for (var i = input.Processed; i < end; i++)
            {
                guarded.Charge(1);
                var item = raw[i];
                if (IsImport(item) && (!allowed || input.Parent is not null)) continue;
                if (importsOnly && item.Kind == CssRuleKind.QualifiedRule)
                {
                    if (!CssParser.HasValidSelector(item, guarded)) continue;
                    allowed = false;
                    end = i;
                    break;
                }
                var parsed = CssParser.ParseRule(item, guarded, input.Parent as CssStyleRule);
                if (parsed is not { } value) continue;
                var rule = value.Rule;
                if (rule is CssImportRule) hasImport = true;
                else if (rule is not CssLayerStatementRule || hasImport) allowed = false;
                rule.Attach(input.Sheet, input.Parent, guarded);
                if (value.Children is { } body)
                    RuleSources.GetValue(rule.Rules, static _ => new()).Input =
                        new(input.Sheet, rule, rule.Rules.Stamp, null, body, 0);
                if (value.Media is { } media)
                {
                    var list = rule switch { CssMediaRule mediaRule => mediaRule.Media, CssImportRule import => import.Media, _ => throw new InvalidOperationException() };
                    MediaSources.GetValue(list, static _ => new()).Input = new(media, list.Stamp);
                }
                result.Add(rule);
                if (importsOnly && !allowed) { end = i + 1; break; }
            }
            guarded.Charge(result.Count);
            rules.Reserve(result.Count);
            guarded.CheckCancellation();
            rules.PublishParsed(result);
            input.Rules = raw;
            input.Processed = end;
            input.ImportsAllowed = allowed;
            input.HasImport = hasImport;
            input.ImportsRead |= importsOnly;
            if (end == raw.Length) slot.Input = null;
            return rules;
        }
        finally { slot.Reading = false; }
    }

    internal static CssMediaList ReadMedia(CssMediaList media, CssValueWork work)
    {
        if (!MediaSources.TryGetValue(media, out var slot) || slot.Input is not { } input) return media;
        // An explicit CSSOM setter is authoritative over an older owner-attribute source.
        if (media.Stamp != input.Stamp) { slot.Input = null; return media; }
        if (slot.Reading) throw new InvalidOperationException("A media list cannot be parsed reentrantly.");
        var guarded = CssValueWork.Guard(work, () =>
        {
            work.CheckCancellation();
            if (!ReferenceEquals(slot.Input, input) || !input.Stamp.CanReuse || media.Stamp != input.Stamp)
                throw new InvalidOperationException(NativeCssQuery.Invalidated);
        });
        slot.Reading = true;
        try
        {
            var parsed = CssParser.ParseMediaQueryList(input.Source, guarded);
            guarded.CheckCancellation();
            media.Initialize(parsed);
            slot.Input = null;
            return media;
        }
        finally { slot.Reading = false; }
    }

    internal static void SetMediaSource(CssMediaList media, string source, CssValueWork work)
    {
        work.Charge(source.Length);
        var slot = MediaSources.GetValue(media, static _ => new());
        var input = new MediaInput(CssSourceText.From(source), default);
        work.CheckCancellation();
        media.Reset();
        input.Stamp = media.Stamp;
        slot.Input = input;
    }

    internal static void ReadTree(CssRuleList rules, CssValueWork work)
    {
        var pending = new Stack<CssRuleList>();
        pending.Push(rules);
        while (pending.TryPop(out var list))
            foreach (var rule in ReadRules(list, work))
            {
                work.Charge(1);
                ReadRuleMedia(rule, work);
                if (rule is CssGroupingRule or CssStyleRule) pending.Push(rule.Rules);
            }
        work.CheckCancellation();
    }

    internal static void ReadRule(CssRule rule, CssValueWork work)
    {
        ReadRuleMedia(rule, work);
        ReadTree(rule.Rules, work);
    }

    private static void ReadRuleMedia(CssRule rule, CssValueWork work)
    {
        switch (rule)
        {
            case CssMediaRule media: ReadMedia(media.Media, work); break;
            case CssImportRule import: ReadMedia(import.Media, work); break;
        }
    }

    internal static void PrepareImports(CssStyleSheet sheet, CssValueWork work)
    {
        Stack<CssStyleSheet>? pending = null;
        HashSet<CssStyleSheet>? seen = null;
        CssStyleSheet? current = sheet;
        while (current is not null)
        {
            work.Charge(1);
            foreach (var rule in ImportRules(current, work))
            {
                work.Charge(1);
                if (rule is CssLayerStatementRule) continue;
                if (rule is not CssImportRule import) break;
                if (import.StyleSheet is not { } child) continue;
                pending ??= new();
                seen ??= new(ReferenceEqualityComparer.Instance) { sheet };
                if (seen.Add(child)) pending.Push(child);
            }
            current = pending is not null && pending.TryPop(out var next) ? next : null;
        }
    }

    internal static CssRule[] ApplicableRules(CssStyleSheet sheet, CssMediaEnvironment environment, CssValueWork work)
    {
        work.CheckCancellation();
        if (sheet.Disabled || !ReadMedia(sheet.Media, work).Matches(environment, work)) return [];
        var result = new List<CssRule>();
        var active = new HashSet<CssStyleSheet>(ReferenceEqualityComparer.Instance) { sheet };
        var frames = new Stack<(CssRuleList Rules, int Index, CssStyleSheet? Sheet)>();
        frames.Push((ReadRules(sheet.Rules, work), 0, sheet));
        while (frames.TryPop(out var frame))
        {
            work.Charge(1);
            if (frame.Index == frame.Rules.Count)
            {
                if (frame.Sheet is { } finished) active.Remove(finished);
                continue;
            }
            var rule = frame.Rules[frame.Index];
            frames.Push((frame.Rules, frame.Index + 1, frame.Sheet));
            result.Add(rule);
            switch (rule)
            {
                case CssStyleRule or CssLayerBlockRule:
                    frames.Push((ReadRules(rule.Rules, work), 0, null));
                    break;
                case CssMediaRule media when ReadMedia(media.Media, work).Matches(environment, work):
                    frames.Push((ReadRules(media.Rules, work), 0, null));
                    break;
                case CssSupportsRule { Matches: true } supports:
                    frames.Push((ReadRules(supports.Rules, work), 0, null));
                    break;
                case CssImportRule { StyleSheet: { } child } when !child.Disabled &&
                    ReadMedia(child.Media, work).Matches(environment, work) && active.Add(child):
                    frames.Push((ReadRules(child.Rules, work), 0, child));
                    break;
            }
        }
        work.Charge(result.Count);
        work.CheckCancellation();
        return result.ToArray();
    }

    private static bool IsImport(CssRawRule rule) =>
        rule.Kind == CssRuleKind.AtRule && CssAscii.EqualsIgnoreCase(rule.Name, "import");
}
