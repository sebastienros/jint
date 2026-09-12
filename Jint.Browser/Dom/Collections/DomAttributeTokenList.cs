using System.Collections;
using System.Runtime.CompilerServices;
using AngleSharp.Dom;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#interface-domtokenlist">DOM §7.1</a>'s token set over an element's
/// content attribute, for the one attribute AngleSharp reflects no <c>ITokenList</c> for.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is not a second token list.</b> Every member a page reaches is still
/// <see cref="DomTokenListMembers"/>' — the validation steps, <c>toggle</c>'s three-state <c>force</c>,
/// <c>replace</c>, <c>supports</c>, the update steps and the stringifier — and this supplies only the token
/// set those members read and write, which for every other <c>DOMTokenList</c> in this package is
/// AngleSharp's own. It exists because
/// <a href="https://svgwg.org/svg2-draft/linking.html#InterfaceSVGAElement">SVG 2 §16.2</a> gives
/// <c>SVGAElement</c> a <c>rel</c>/<c>relList</c> pair and AngleSharp builds a bare
/// <c>AngleSharp.Svg.Dom.SvgElement</c> for an SVG <c>&lt;a&gt;</c> with no interface of its own and no
/// public way to build a list over an arbitrary attribute: <c>ClassList</c> and the three
/// <c>RelationList</c>s are the only members in the pinned assemblies that answer one, and every
/// implementation of the interface is <c>internal</c>. <c>Dom/divergences.md</c> records it, and
/// <c>DomStringMapAdapter</c> is the same shape of answer for <c>DOMStringMap</c>.
/// </para>
/// <para>
/// <b>The attribute is the storage, and it is read on every access.</b> Nothing is cached between calls, so
/// the list is live against a page that writes the content attribute directly, an
/// <c>Element.setAttribute</c> from anywhere, and the parser — which is what AngleSharp's own list is, and
/// what §7.1 requires of one.
/// </para>
/// </remarks>
internal sealed class DomAttributeTokenList : ITokenList
{
    /// <summary>https://infra.spec.whatwg.org/#ascii-whitespace: TAB, LF, FF, CR and SPACE, and nothing else.</summary>
    private static readonly char[] _asciiWhitespace = ['\t', '\n', '\f', '\r', ' '];

    /// <summary>
    /// The <c>rel</c> list of each element that has been asked for one, so that WebIDL's
    /// <a href="https://webidl.spec.whatwg.org/#SameObject"><c>[SameObject]</c></a> holds:
    /// <c>svgA.relList === svgA.relList</c>. The wrapper cache keys on the CLR object, so one instance per
    /// element is all the identity needs. A <see cref="ConditionalWeakTable{TKey,TValue}"/> rather than a
    /// field for the reason every other table over an AngleSharp object here has one: this assembly cannot
    /// add a field to <c>SvgElement</c>.
    /// </summary>
    private static readonly ConditionalWeakTable<IElement, DomAttributeTokenList> _relLists = new();

    private readonly IElement _element;
    private readonly string _attribute;

    private DomAttributeTokenList(IElement element, string attribute)
    {
        _element = element;
        _attribute = attribute;
    }

    /// <summary>The <c>rel</c> token set of <paramref name="element"/>, the same instance on every read.</summary>
    internal static ITokenList Rel(IElement element)
        => _relLists.GetValue(element, static owner => new DomAttributeTokenList(owner, "rel"));

    public int Length => Tokens().Count;

    public string this[int index] => Tokens()[index];

    public bool Contains(string token) => Tokens().Contains(token, StringComparer.Ordinal);

    public void Add(params string[] tokens)
    {
        var set = Tokens();
        var changed = false;

        foreach (var token in tokens)
        {
            if (!set.Contains(token, StringComparer.Ordinal))
            {
                set.Add(token);
                changed = true;
            }
        }

        if (changed)
        {
            Write(set);
        }
    }

    public void Remove(params string[] tokens)
    {
        var set = Tokens();
        var changed = false;

        foreach (var token in tokens)
        {
            changed |= set.RemoveAll(existing => string.Equals(existing, token, StringComparison.Ordinal)) > 0;
        }

        if (changed)
        {
            Write(set);
        }
    }

    public bool Toggle(string token, bool force)
    {
        if (Contains(token))
        {
            if (force)
            {
                return true;
            }

            Remove(token);
            return false;
        }

        if (!force)
        {
            return false;
        }

        Add(token);
        return true;
    }

    public IEnumerator<string> GetEnumerator() => Tokens().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// https://infra.spec.whatwg.org/#ordered-sets — the attribute split on ASCII whitespace with
    /// duplicates dropped, which is what <a href="https://dom.spec.whatwg.org/#concept-ordered-set-parser">
    /// DOM's ordered set parser</a> makes of a content attribute.
    /// </summary>
    private List<string> Tokens()
    {
        var declared = _element.GetAttribute(_attribute);

        if (string.IsNullOrEmpty(declared))
        {
            return [];
        }

        var set = new List<string>();

        foreach (var token in declared!.Split(_asciiWhitespace, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!set.Contains(token, StringComparer.Ordinal))
            {
                set.Add(token);
            }
        }

        return set;
    }

    /// <summary>
    /// https://infra.spec.whatwg.org/#ordered-set-serializer. <c>DomTokenListMembers.Update</c> writes the
    /// attribute again on the way out, which is §7.1's update steps and is deliberately not conditional; this
    /// write is what keeps the set the next read parses in step with the one a member just changed.
    /// </summary>
    private void Write(List<string> tokens) => _element.SetAttribute(_attribute, string.Join(" ", tokens));
}
