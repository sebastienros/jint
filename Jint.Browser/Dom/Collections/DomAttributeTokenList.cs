using System.Collections;
using System.Runtime.CompilerServices;
using Jint.HtmlParser;

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
internal sealed class DomAttributeTokenList : IEnumerable<string>
{
    /// <summary>
    /// The <c>rel</c> list of each element that has been asked for one, so that WebIDL's
    /// <a href="https://webidl.spec.whatwg.org/#SameObject"><c>[SameObject]</c></a> holds:
    /// <c>svgA.relList === svgA.relList</c>. The wrapper cache keys on the CLR object, so one instance per
    /// element is all the identity needs. A <see cref="ConditionalWeakTable{TKey,TValue}"/> rather than a
    /// field for the reason every other table over an AngleSharp object here has one: this assembly cannot
    /// add a field to <c>SvgElement</c>.
    /// </summary>
    private static readonly ConditionalWeakTable<Element, Dictionary<string, DomAttributeTokenList>> _lists = new();

    private readonly Element _element;
    private readonly string _attribute;

    private DomAttributeTokenList(Element element, string attribute)
    {
        _element = element;
        _attribute = attribute;
    }

    /// <summary>The <c>rel</c> token set of <paramref name="element"/>, the same instance on every read.</summary>
    internal static DomAttributeTokenList Rel(Element element) => Of(element, "rel");

    internal static DomAttributeTokenList Of(Element element, string attribute)
    {
        var lists = _lists.GetValue(element, static _ => new(StringComparer.Ordinal));
        if (!lists.TryGetValue(attribute, out var list))
        {
            list = new DomAttributeTokenList(element, attribute);
            lists.Add(attribute, list);
        }
        return list;
    }

    internal Element Element => _element;
    internal string Attribute => _attribute;
    internal string Value
    {
        get => _element.GetAttributeNS(null, _attribute) ?? "";
        set => _element.SetAttributeNS(null, _attribute, value);
    }

    public int Length => ReadLength(null, default);

    public string this[int index] => index < 0 ? throw new ArgumentOutOfRangeException(nameof(index))
        : ReadItem((uint) index, null, default) ?? throw new ArgumentOutOfRangeException(nameof(index));

    public bool Contains(string token) => ReadContains(token, null, default);

    internal int ReadLength(Action<int>? checkpoint, CancellationToken token)
        => ReadLength(new DomReadWork(checkpoint, token));

    internal int ReadLength(DomReadWork work)
    {
        var count = 0;
        foreach (var unused in Slices(work)) count++;
        return count;
    }

    internal string? ReadItem(uint index, Action<int>? checkpoint, CancellationToken token)
        => ReadItem(index, new DomReadWork(checkpoint, token));

    internal string? ReadItem(uint index, DomReadWork work)
    {
        foreach (var slice in Slices(work))
        {
            if (index-- != 0) continue;
            return slice.Materialize();
        }
        return null;
    }

    internal bool ReadContains(string token, Action<int>? checkpoint, CancellationToken cancellationToken)
        => ReadContains(token, new DomReadWork(checkpoint, cancellationToken));

    internal bool ReadContains(string token, DomReadWork work)
    {
        foreach (var slice in Slices(work)) if (slice.Matches(token, work)) return true;
        return false;
    }

    internal string ReadValue(Action<int>? checkpoint, CancellationToken token)
        => ReadValue(new DomReadWork(checkpoint, token));

    internal string ReadValue(DomReadWork work)
    {
        work.Check();
        var value = work.Attribute(_element, _attribute) ?? "";
        work.Check();
        return value;
    }

    internal IEnumerable<string> Read(Action<int>? checkpoint, CancellationToken token)
        => Read(new DomReadWork(checkpoint, token));

    internal IEnumerable<string> Read(DomReadWork work)
    {
        foreach (var slice in Slices(work)) yield return slice.Materialize();
    }

    internal List<string> ReadSnapshot(DomReadWork work)
    {
        var result = new List<string>();
        foreach (var token in Read(work)) result.Add(token);
        return result;
    }

    internal List<string> ReadSnapshot(Action<int>? checkpoint, CancellationToken token)
        => ReadSnapshot(new DomReadWork(checkpoint, token));

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

    public IEnumerator<string> GetEnumerator() => Read(null, default).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// https://infra.spec.whatwg.org/#ordered-sets — the attribute split on ASCII whitespace with
    /// duplicates dropped, which is what <a href="https://dom.spec.whatwg.org/#concept-ordered-set-parser">
    /// DOM's ordered set parser</a> makes of a content attribute.
    /// </summary>
    private List<string> Tokens() => ReadSnapshot(new DomReadWork(null, default));

    private IEnumerable<TokenSlice> Slices(DomReadWork work)
    {
        work.Check();
        var declared = work.Attribute(_element, _attribute) ?? "";
        var seen = new HashSet<TokenSlice>(new SliceComparer(work));
        var start = -1;
        uint hash = 2166136261;
        try
        {
            for (var position = 0; position <= declared.Length; position++)
            {
                if (position < declared.Length)
                {
                    work.Step();
                    var c = declared[position];
                    if (c is not ('\t' or '\n' or '\f' or '\r' or ' '))
                    {
                        if (start < 0) { start = position; hash = 2166136261; }
                        hash = unchecked((hash ^ c) * 16777619);
                        continue;
                    }
                }
                if (start < 0) continue;
                var slice = new TokenSlice(declared, start, position - start, hash);
                start = -1;
                if (seen.Add(slice)) yield return slice;
            }
        }
        finally { work.Check(); }
    }

    private readonly struct TokenSlice(string source, int start, int length, uint hash)
    {
        private readonly string _source = source;
        private readonly int _start = start;
        internal int Length { get; } = length;
        internal uint Hash { get; } = hash;
        internal string Materialize() => _source.Substring(_start, Length);
        internal bool Matches(string token, DomReadWork work)
        {
            work.Step();
            if (Length != token.Length) return false;
            for (var i = 0; i < Length; i++)
            {
                work.Step();
                if (_source[_start + i] != token[i]) return false;
            }
            return true;
        }
        internal bool Matches(TokenSlice other, DomReadWork work)
        {
            work.Step();
            if (Length != other.Length) return false;
            for (var i = 0; i < Length; i++)
            {
                work.Step();
                if (_source[_start + i] != other._source[other._start + i]) return false;
            }
            return true;
        }
    }

    private sealed class SliceComparer(DomReadWork work) : IEqualityComparer<TokenSlice>
    {
        public bool Equals(TokenSlice x, TokenSlice y) => x.Matches(y, work);
        public int GetHashCode(TokenSlice value) => unchecked((int) value.Hash);
    }

    /// <summary>
    /// https://infra.spec.whatwg.org/#ordered-set-serializer. <c>DomTokenListMembers.Update</c> writes the
    /// attribute again on the way out, which is §7.1's update steps and is deliberately not conditional; this
    /// write is what keeps the set the next read parses in step with the one a member just changed.
    /// </summary>
    private void Write(List<string> tokens) => _element.SetAttributeNS(null, _attribute, string.Join(" ", tokens));
}
