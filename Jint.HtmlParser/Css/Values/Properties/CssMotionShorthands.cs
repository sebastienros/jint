using System.Globalization;
using Jint.HtmlParser.Css.Syntax;
using Item = Jint.HtmlParser.Css.Values.Properties.CssBoxShorthands.Item;

namespace Jint.HtmlParser.Css.Values.Properties;

// CSS Animations 1 §3.9 (animation) and CSS Transitions 1 §2.5 with Level 2's transition-behavior:
// comma-separated layers whose components are order-free, so whitespace splitting cannot place them.
// https://drafts.csswg.org/css-animations-1/#animation and https://drafts.csswg.org/css-transitions-1/#transition-shorthand-property
// Renderless: a component is classified by its token shape only; keywords are lowered, other components keep their
// declared text. Each longhand holds one comma-separated item per layer.
internal static class CssMotionShorthands
{
    // In the order Chromium serializes a layer, which is also the order Expand returns.
    internal static readonly string[] AnimationLonghands =
    [
        "animation-duration", "animation-timing-function", "animation-delay", "animation-iteration-count",
        "animation-direction", "animation-fill-mode", "animation-play-state", "animation-name"
    ];

    internal static readonly string[] AnimationInitials = ["0s", "ease", "0s", "1", "normal", "none", "running", "none"];

    internal static readonly string[] TransitionLonghands =
    [
        "transition-property", "transition-duration", "transition-timing-function", "transition-delay", "transition-behavior"
    ];

    internal static readonly string[] TransitionInitials = ["all", "0s", "ease", "0s", "normal"];

    internal static bool Handles(string name) => name is "animation" or "transition";

    /// <summary>Whether a longhand's items are &lt;time&gt; values, which compute to seconds.</summary>
    internal static bool IsTimeList(string name) =>
        name is "animation-duration" or "animation-delay" or "transition-duration" or "transition-delay";

    /// <summary>The longhand values in registry order, or null when the text is not a valid value.</summary>
    internal static string[]? Expand(string name, string text, CssValueWork work)
    {
        var layers = CssBoxShorthands.Layers(text, work);
        if (layers is null) return null;
        var animation = name == "animation";
        var count = animation ? AnimationLonghands.Length : TransitionLonghands.Length;
        var lists = new List<string>[count];
        for (var i = 0; i < count; i++) lists[i] = new(layers.Count);
        foreach (var items in layers)
        {
            if (items.Count == 0) return null;
            var values = animation ? AnimationLayer(items, work) : TransitionLayer(items, work);
            if (values is null) return null;
            // CSS Transitions 1 §2.1: none is valid only as the sole value of transition-property.
            if (!animation && values[0] == "none" && layers.Count > 1) return null;
            for (var i = 0; i < count; i++)
            {
                work.Charge(1);
                lists[i].Add(values[i]);
            }
        }
        var result = new string[count];
        for (var i = 0; i < count; i++) result[i] = Joined(lists[i], work, ", ");
        return result;
    }

    /// <summary>
    /// The shortest text that expands back to these values in Chromium's component order, or empty when there is none.
    /// A computed value repeats shorter longhand lists to the layer count (CSS Animations 1 §3, CSS Transitions 1 §2);
    /// a specified value with lists of different lengths has no shorthand.
    /// </summary>
    internal static string Serialize(string name, string[] values, bool computed, CssValueWork work)
    {
        var animation = name == "animation";
        var lists = new string[values.Length][];
        for (var i = 0; i < values.Length; i++) lists[i] = CssBoxShorthands.SplitLayers(values[i], work);
        // The layer count is animation-name's (CSS Animations 1 §3) or transition-property's (CSS Transitions 1 §2).
        var layerCount = lists[animation ? values.Length - 1 : 0].Length;
        foreach (var list in lists)
        {
            work.Charge(1);
            if (list.Length != layerCount && !computed) return "";
        }
        var layers = new List<string>(layerCount);
        var parts = new List<string>(values.Length);
        var row = new string[values.Length];
        for (var layer = 0; layer < layerCount; layer++)
        {
            work.Charge(values.Length);
            for (var i = 0; i < row.Length; i++) row[i] = lists[i][layer % lists[i].Length];
            parts.Clear();
            if (animation) SerializeAnimation(parts, row, work);
            else SerializeTransition(parts, row, work);
            layers.Add(Joined(parts, work));
        }
        return Joined(layers, work, ", ");
    }

    /// <summary>
    /// CSS Values 4 §7.1: a computed &lt;time&gt; is in seconds, the canonical unit, written like Chromium with six
    /// significant digits; other text is kept.
    /// </summary>
    internal static string ComputedTimes(string text, CssValueWork work)
    {
        var items = CssBoxShorthands.SplitLayers(text, work);
        var changed = false;
        for (var i = 0; i < items.Length; i++)
        {
            work.Charge(1);
            if (Seconds(items[i]) is not { } seconds) continue;
            // -0 serializes as 0.
            var canonical = (seconds == 0 ? 0 : seconds).ToString("G6", CultureInfo.InvariantCulture).Replace('E', 'e') + "s";
            if (canonical == items[i]) continue;
            items[i] = canonical;
            changed = true;
        }
        return changed ? Joined(items, work, ", ") : text;
    }

    // Chromium writes a layer's duration whenever its delay is written, since the first <time> is the duration.
    private static void SerializeAnimation(List<string> parts, string[] row, CssValueWork work)
    {
        var name = row[7];
        var none = Is(name, "none");
        // A name that another component would claim on reparsing is written after every other component.
        var full = !none && IsAnimationKeyword(Lowered(name, work));
        if (full || !IsZeroTime(row[0]) || !IsZeroTime(row[2])) parts.Add(row[0]);
        if (full || !Is(row[1], "ease")) parts.Add(row[1]);
        if (full || !IsZeroTime(row[2])) parts.Add(row[2]);
        if (full || row[3] != "1") parts.Add(row[3]);
        if (full || !Is(row[4], "normal")) parts.Add(row[4]);
        if (full || !Is(row[5], "none")) parts.Add(row[5]);
        if (full || !Is(row[6], "running")) parts.Add(row[6]);
        if (!none || parts.Count == 0) parts.Add(none ? "none" : name);
    }

    private static void SerializeTransition(List<string> parts, string[] row, CssValueWork work)
    {
        var property = row[0];
        // A property named like an easing or behavior keyword is written last, after both, so it reparses as the property.
        var full = IsTransitionKeyword(Lowered(property, work));
        if (!full && !Is(property, "all")) parts.Add(property);
        if (full || !IsZeroTime(row[1]) || !IsZeroTime(row[3])) parts.Add(row[1]);
        if (full || !Is(row[2], "ease")) parts.Add(row[2]);
        if (full || !IsZeroTime(row[3])) parts.Add(row[3]);
        if (full || !Is(row[4], "normal")) parts.Add(row[4]);
        if (full) parts.Add(property);
        if (parts.Count == 0) parts.Add("all");
    }

    // <single-animation>: the first <time> is the duration and the second the delay; a keyword goes to the first
    // component that accepts it and has no value yet, and only then to animation-name (CSS Animations 1 §3.9).
    private static string[]? AnimationLayer(List<Item> items, CssValueWork work)
    {
        var values = new string?[AnimationLonghands.Length];
        foreach (var item in items)
        {
            work.Charge(1);
            var keyword = item.Keyword;
            if (IsTime(item, work))
            {
                if (!Times(values, 0, 2, item, work)) return null;
            }
            else if (values[1] is null && IsEasing(item)) values[1] = Canonical(item);
            else if (values[3] is null && IsIterationCount(item, work)) values[3] = CssText.WithLeadingZero(Canonical(item));
            else if (values[4] is null && IsDirection(keyword)) values[4] = keyword;
            else if (values[5] is null && IsFillMode(keyword)) values[5] = keyword;
            else if (values[6] is null && IsPlayState(keyword)) values[6] = keyword;
            else if (values[7] is null && keyword == "none") values[7] = keyword;
            else if (values[7] is null && IsName(item)) values[7] = item.Text;
            else return null;
        }
        return Filled(values, AnimationInitials, work);
    }

    // <single-transition> = [ none | <single-transition-property> ] || <time> || <easing-function> || <time> ||
    // <transition-behavior-value>.
    private static string[]? TransitionLayer(List<Item> items, CssValueWork work)
    {
        var values = new string?[TransitionLonghands.Length];
        foreach (var item in items)
        {
            work.Charge(1);
            var keyword = item.Keyword;
            if (IsTime(item, work))
            {
                if (!Times(values, 1, 3, item, work)) return null;
            }
            else if (values[2] is null && IsEasing(item)) values[2] = Canonical(item);
            else if (values[4] is null && keyword is "normal" or "allow-discrete") values[4] = keyword;
            else if (values[0] is null && keyword is "none" or "all") values[0] = keyword;
            // Property names are ASCII case-insensitive; a custom property's name is not.
            else if (values[0] is null && IsName(item))
                values[0] = item.Text.StartsWith("--", StringComparison.Ordinal) ? item.Text : keyword;
            else return null;
        }
        return Filled(values, TransitionInitials, work);
    }

    // The first <time> is the duration and the second the delay, but the duration is a <time [0s,∞]>: a negative
    // time cannot be one, so it is the delay (CSS Animations 1 §3.9, CSS Transitions 1 §2.5).
    private static bool Times(string?[] values, int duration, int delay, Item item, CssValueWork work)
    {
        var text = CssText.WithLeadingZero(item.Text);
        var negative = item.Value.TokenKind == CssTokenKind.Dimension &&
            CssNumber.FromValidatedToken(item.Value.Token.NumberText, work).Sign < 0;
        if (values[duration] is null && !negative) values[duration] = text;
        else if (values[delay] is null) values[delay] = text;
        else return false;
        return true;
    }

    private static string[] Filled(string?[] values, string[] initials, CssValueWork work)
    {
        var result = new string[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            work.Charge(1);
            result[i] = values[i] ?? initials[i];
        }
        return result;
    }

    // Whether reparsing a lone keyword would assign it to a component other than the name or property.
    private static bool IsAnimationKeyword(string keyword) =>
        IsEasingKeyword(keyword) || keyword == "infinite" || IsDirection(keyword) || IsFillMode(keyword) || IsPlayState(keyword);

    private static bool IsTransitionKeyword(string keyword) => IsEasingKeyword(keyword) || keyword is "normal" or "allow-discrete";

    private static bool IsEasingKeyword(string keyword) =>
        keyword is "linear" or "ease" or "ease-in" or "ease-out" or "ease-in-out" or "step-start" or "step-end";
    private static bool IsDirection(string keyword) => keyword is "normal" or "reverse" or "alternate" or "alternate-reverse";
    private static bool IsFillMode(string keyword) => keyword is "none" or "forwards" or "backwards" or "both";
    private static bool IsPlayState(string keyword) => keyword is "running" or "paused";

    // CSS Easing 2 §2: <easing-function> = linear | <linear-easing-function> | <cubic-bezier-easing-function> |
    // <step-easing-function>.
    private static bool IsEasing(Item item)
    {
        if (IsEasingKeyword(item.Keyword)) return true;
        if (item.Value.Kind != CssComponentKind.Function) return false;
        var function = item.Value.FunctionName;
        return CssAscii.EqualsIgnoreCase(function, "cubic-bezier") || CssAscii.EqualsIgnoreCase(function, "steps") ||
            CssAscii.EqualsIgnoreCase(function, "linear");
    }

    // <single-animation-iteration-count> = infinite | <number [0,∞]>.
    private static bool IsIterationCount(Item item, CssValueWork work) =>
        item.Keyword == "infinite" ||
        item.Value.Kind == CssComponentKind.Function && MathType(item.Value, work) == CssUnitCategory.None ||
        item.Value.TokenKind == CssTokenKind.Number && CssNumber.FromValidatedToken(item.Value.Token.NumberText, work).Sign >= 0;

    // A <time> dimension, or a math function whose arguments resolve to one.
    private static bool IsTime(Item item, CssValueWork work) => item.Value.Kind == CssComponentKind.Function
        ? MathType(item.Value, work) == CssUnitCategory.Time
        : item.Value.TokenKind == CssTokenKind.Dimension &&
          CssUnits.Recognize(item.Value.Token.Unit, work).Category() == CssUnitCategory.Time;

    // Renderless type check of a math function: None when it holds only numbers, Time when its dimensions are all times,
    // null when it is not a math function or mixes in a length, percentage or other dimension.
    private static CssUnitCategory? MathType(CssComponentValue function, CssValueWork work)
    {
        if (!CssLayoutValues.Math(function)) return null;
        var type = CssUnitCategory.None;
        Stack<CssComponentValueList>? pending = null;
        var list = function.Values;
        while (true)
        {
            foreach (var inner in list)
            {
                work.Charge(1);
                if (inner.Kind == CssComponentKind.Function)
                {
                    if (!CssLayoutValues.Math(inner)) return null;
                    (pending ??= new()).Push(inner.Values);
                }
                else if (inner.Kind == CssComponentKind.SimpleBlock) (pending ??= new()).Push(inner.Values);
                else if (inner.TokenKind == CssTokenKind.Percentage) return null;
                else if (inner.TokenKind == CssTokenKind.Dimension)
                {
                    if (CssUnits.Recognize(inner.Token.Unit, work).Category() != CssUnitCategory.Time) return null;
                    type = CssUnitCategory.Time;
                }
            }
            if (pending is null || !pending.TryPop(out list!)) return type;
        }
    }

    // <keyframes-name> = <custom-ident> | <string>; <single-transition-property> = all | <custom-ident>.
    // CSS Values 4 §4.2: a <custom-ident> excludes the CSS-wide keywords and default.
    private static bool IsName(Item item) =>
        item.Value.TokenKind == CssTokenKind.String ||
        item.Value.TokenKind == CssTokenKind.Ident && !CssText.IsWide(item.Keyword) && item.Keyword != "default";

    private static string Canonical(Item item) => item.Keyword.Length != 0 ? item.Keyword : item.Text;

    private static bool Is(string text, string keyword) => CssAscii.EqualsIgnoreCase(text, keyword);

    private static string Lowered(string text, CssValueWork work)
    {
        work.Charge(text.Length);
        return text.AsSpan().ContainsAnyInRange('A', 'Z') ? text.ToLowerInvariant() : text;
    }

    private static bool IsZeroTime(string text) => Seconds(text) == 0;

    // The seconds an s or ms dimension spells, or null for any other text.
    private static double? Seconds(string text)
    {
        var divisor = 1.0;
        int length;
        if (text.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
        {
            divisor = 1000;
            length = text.Length - 2;
        }
        else if (text.EndsWith('s') || text.EndsWith('S')) length = text.Length - 1;
        else return null;
        if (length == 0 || !char.IsAsciiDigit(text[length - 1])) return null;
        return double.TryParse(text.AsSpan(0, length), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint |
                NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? number / divisor
            : null;
    }

    private static string Joined(IEnumerable<string> parts, CssValueWork work, string separator = " ")
    {
        var result = string.Join(separator, parts);
        work.Charge(result.Length);
        return result;
    }
}
