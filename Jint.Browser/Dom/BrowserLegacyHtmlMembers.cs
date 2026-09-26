using System.Globalization;
using System.Runtime.CompilerServices;
using Jint.Browser.Dom.Collections;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom;

/// <summary>Finite legacy HTML members retained by the pinned binding surface.</summary>
internal static class BrowserLegacyHtmlMembers
{
    private static readonly ConditionalWeakTable<Element, EmptyKeygenLabels> KeygenLabelViews = new();

    // AngleSharp 1.8.2 allocates but never populates this legacy label list.
    internal static DomNodeList KeygenLabels(Element element)
        => KeygenLabelViews.GetValue(element, static _ => new EmptyKeygenLabels());

    // AngleSharp 1.8.2 HtmlMenuItemElement.Command: nonempty raw ID, first document match, HTML brand.
    internal static Element? Command(DomRealm realm, Element element)
    {
        var work = Work(realm);
        Element? result = null;
        if (work.Attribute(element, "command") is { Length: > 0 } id && element.OwnerDocument is { } document)
        {
            foreach (var candidate in NodeTraversal.DescendantElements(document, work.Check, work.Token))
            {
                if (!work.Equal(work.Attribute(candidate, "id"), id)) continue;
                if (candidate.NamespaceUri == Namespaces.Html) result = candidate;
                break;
            }
        }
        work.Check();
        return result;
    }

    // https://html.spec.whatwg.org/multipage/obsolete.html#dom-marquee-loop
    internal static int GetMarqueeLoop(DomRealm realm, Element element)
    {
        var work = Work(realm);
        var value = ParseLoop(work.Attribute(element, "loop"), work);
        work.Check();
        return value;
    }

    internal static JsValue SetMarqueeLoop(DomRealm realm, Element element, int value)
    {
        var work = Work(realm);
        if (value > 0 || value == -1)
        {
            var current = ParseLoop(work.Attribute(element, "loop"), work);
            work.Check();
            if (current != value) element.SetAttributeNS(null, "loop", value.ToString(CultureInfo.InvariantCulture));
        }
        work.Check();
        return JsValue.Undefined;
    }

    private static int ParseLoop(string? raw, DomReadWork work)
    {
        if (raw is null) return -1;
        var position = 0;
        while (position < raw.Length)
        {
            work.Step();
            if (raw[position] is not (' ' or '\t' or '\n' or '\r' or '\f')) break;
            position++;
        }
        if (position == raw.Length) return -1;
        var negative = raw[position] == '-';
        if (raw[position] is '+' or '-') position++;
        var start = position;
        var value = 0;
        var overflow = false;
        while (position < raw.Length)
        {
            work.Step();
            var c = raw[position];
            if (c is < '0' or > '9') break;
            if (!overflow)
            {
                var digit = c - '0';
                overflow = value > (int.MaxValue - digit) / 10;
                if (!overflow) value = value * 10 + digit;
            }
            position++;
        }
        // HTML does not specify an out-of-Int32 result for this long IDL attribute.
        // This finite interoperability policy uses the same -1 fallback as an invalid count.
        return position == start || negative || overflow || value < 1 ? -1 : value;
    }

    private static DomReadWork Work(DomRealm realm)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        return work;
    }

    private sealed class EmptyKeygenLabels : DomNodeList
    {
        internal override int Length => 0;
        internal override Node this[int index] => throw new ArgumentOutOfRangeException(nameof(index));
        internal override int ReadLength(Action<int>? checkpoint, CancellationToken token)
        {
            var work = new DomReadWork(checkpoint, token);
            work.Check();
            return 0;
        }
        internal override Node? ReadItem(uint index, Action<int>? checkpoint, CancellationToken token)
        {
            var work = new DomReadWork(checkpoint, token);
            work.Check();
            return null;
        }
    }
}
