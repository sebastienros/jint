using System.Collections;
using System.Runtime.CompilerServices;
using Jint.HtmlParser;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// <a href="https://dom.spec.whatwg.org/#interface-domtokenlist">DOM §7.1</a>'s token set over an element's
/// current native content attribute.
/// </summary>
/// <remarks>
/// <para>
/// <b>The raw attribute is the storage.</b> Every member a page reaches is still
/// <see cref="DomTokenListMembers"/>' — the validation steps, <c>toggle</c>'s three-state <c>force</c>,
/// <c>replace</c>, <c>supports</c>, the update steps and the stringifier — and this supplies only the token
/// set those members read and write. Work, token scanning and duplicate comparisons belong to one
/// invocation, and no callback or realm is retained by the list.
/// </para>
/// <para>
/// <b>The source is read on every access.</b> The list stays live against direct native attribute writes,
/// script mutation and adoption; no serialized token set becomes another authoritative store.
/// </para>
/// </remarks>
internal sealed class DomAttributeTokenList : IEnumerable<string>
{
    /// <summary>
    /// The <c>rel</c> list of each element that has been asked for one, so that WebIDL's
    /// <a href="https://webidl.spec.whatwg.org/#SameObject"><c>[SameObject]</c></a> holds:
    /// <c>svgA.relList === svgA.relList</c>. The wrapper cache keys on the CLR object, so one instance per
    /// element is all the identity needs. A <see cref="ConditionalWeakTable{TKey,TValue}"/> rather than a
    /// parser field, so ordinary raw elements allocate no Browser token-list state.
    /// </summary>
    private static readonly ConditionalWeakTable<Element, Dictionary<string, DomAttributeTokenList>> _lists = new();

    private readonly Element _element;
    private readonly string _attribute;
    private string? _indexedSource;
    private TokenSlice[]? _indexedTokens;
    private string?[]? _indexedStrings;

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
        => Index(work).Tokens.Length;

    internal string? ReadItem(uint index, Action<int>? checkpoint, CancellationToken token)
        => ReadItem(index, new DomReadWork(checkpoint, token));

    internal string? ReadItem(uint index, DomReadWork work)
    {
        while (true)
        {
            var read = Index(work);
            if (index >= (uint) read.Tokens.Length) return null;
            var itemIndex = (int) index;
            // Allocate privately before the final check. An interrupted copy publishes nothing.
            var strings = _indexedStrings ?? new string?[read.Tokens.Length];
            var result = strings[itemIndex] ?? read.Tokens[itemIndex].Materialize(read.Proof.Value!, work);
            work.Check();
            if (!read.Proof.IsCurrent) continue;
            // A checkpoint may have reentered a read and installed another current index.
            if (ReferenceEquals(_indexedTokens, read.Tokens) && ReferenceEquals(_indexedSource, read.Proof.Value))
            {
                _indexedStrings ??= strings;
                _indexedStrings[itemIndex] = result;
            }
            return result;
        }
    }

    internal bool ReadContains(string token, Action<int>? checkpoint, CancellationToken cancellationToken)
        => ReadContains(token, new DomReadWork(checkpoint, cancellationToken));

    internal bool ReadContains(string token, DomReadWork work)
    {
        while (true)
        {
            var read = Index(work);
            var result = false;
            foreach (var slice in read.Tokens)
            {
                if (!slice.Matches(read.Proof.Value!, token, work)) continue;
                result = true;
                break;
            }
            work.Check();
            if (read.Proof.IsCurrent) return result;
        }
    }

    internal string ReadValue(Action<int>? checkpoint, CancellationToken token)
        => ReadValue(new DomReadWork(checkpoint, token));

    internal string ReadValue(DomReadWork work)
    {
        while (true)
        {
            work.Check();
            var proof = Capture(work);
            work.Check();
            if (proof.IsCurrent) return proof.Value ?? "";
        }
    }

    internal IEnumerable<string> Read(Action<int>? checkpoint, CancellationToken token)
        => Read(new DomReadWork(checkpoint, token));

    internal IEnumerable<string> Read(DomReadWork work)
    {
        var read = Index(work);
        // One enumeration observes one immutable invocation source; indexed JS iteration
        // calls ReadItem anew and therefore observes subsequent attribute mutations.
        try
        {
            foreach (var slice in read.Tokens)
            {
                work.Step();
                yield return slice.Materialize(read.Proof.Value!, work);
            }
        }
        finally { work.Check(); }
    }

    internal List<string> ReadSnapshot(DomReadWork work)
        => ReadSnapshot(work, out _);

    internal List<string> ReadSnapshot(DomReadWork work, out SourceProof proof)
    {
        while (true)
        {
            var read = Index(work);
            var result = new List<string>(read.Tokens.Length);
            foreach (var slice in read.Tokens)
            {
                work.Step();
                result.Add(slice.Materialize(read.Proof.Value!, work));
            }
            work.Check();
            if (!read.Proof.IsCurrent) continue;
            proof = read.Proof;
            return result;
        }
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

    private TokenRead Index(DomReadWork work)
    {
        while (true)
        {
            work.Check();
            var proof = Capture(work);
            var tokens = _indexedTokens is not null && ReferenceEquals(_indexedSource, proof.Value)
                ? _indexedTokens : Build(proof.Value ?? "", work);
            work.Check();
            // No callback follows this validation or the completed-index publication.
            if (!proof.IsCurrent) continue;
            if (!ReferenceEquals(_indexedTokens, tokens)) _indexedStrings = null;
            _indexedSource = proof.Value;
            _indexedTokens = tokens;
            return new TokenRead(proof, tokens);
        }
    }

    private SourceProof Capture(DomReadWork work)
    {
        // A present Attr itself proves the source. Only a miss demands the native
        // structure token, followed by another bounded lookup under that token.
        var attribute = FindAttribute(work);
        if (attribute is not null) return new SourceProof(_element, attribute, attribute.Value, null);
        while (true)
        {
            var structure = _element.GetAttributeStructureIdentity();
            attribute = FindAttribute(work);
            if (attribute is not null) return new SourceProof(_element, attribute, attribute.Value, null);
            if (ReferenceEquals(structure, _element.ExistingAttributeStructureIdentity))
                return new SourceProof(_element, null, null, structure);
        }
    }

    private Attr? FindAttribute(DomReadWork work)
    {
        while (true)
        {
            var structure = _element.ExistingAttributeStructureIdentity;
            var restart = false;
            for (uint index = 0; index < (uint) _element.AttributeCount; index++)
            {
                work.Step();
                var attribute = _element.GetAttributeAt(index);
                if (attribute is null) { restart = true; break; }
                var matches = attribute.NamespaceUri is null && work.Equal(attribute.LocalName, _attribute);
                if (!ReferenceEquals(attribute.OwnerElement, _element)) { restart = true; break; }
                if (matches) return attribute;
                if (!ReferenceEquals(structure, _element.ExistingAttributeStructureIdentity)) { restart = true; break; }
            }
            if (!restart) return null;
        }
    }

    internal readonly struct SourceProof(Element element, Attr? attribute, string? value, object? structure)
    {
        internal string? Value { get; } = value;
        internal bool IsCurrent => attribute is not null
            ? ReferenceEquals(attribute.OwnerElement, element) && ReferenceEquals(attribute.Value, Value)
            : ReferenceEquals(element.ExistingAttributeStructureIdentity, structure);
    }

    private readonly record struct TokenRead(SourceProof Proof, TokenSlice[] Tokens);

    private static TokenSlice[] Build(string declared, DomReadWork work)
    {
        if (declared.Length == 0) return [];
        var seen = new HashSet<TokenSlice>(new SliceComparer(declared, work));
        var tokens = new List<TokenSlice>();
        var start = -1;
        uint hash = 2166136261;
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
            var slice = new TokenSlice(start, position - start, hash);
            start = -1;
            if (seen.Add(slice)) tokens.Add(slice);
        }
        return tokens.ToArray();
    }

    private readonly struct TokenSlice(int start, int length, uint hash)
    {
        private readonly int _start = start;
        internal int Length { get; } = length;
        internal uint Hash { get; } = hash;
        internal string Materialize(string source, DomReadWork work)
        {
            if (_start == 0 && Length == source.Length) return source;
            return string.Create(Length, (source, start: _start, work), static (span, state) =>
            {
                for (var i = 0; i < span.Length; i++)
                {
                    state.work.Step();
                    span[i] = state.source[state.start + i];
                }
            });
        }
        internal bool Matches(string source, string token, DomReadWork work)
        {
            work.Step();
            if (Length != token.Length) return false;
            for (var i = 0; i < Length; i++)
            {
                work.Step();
                if (source[_start + i] != token[i]) return false;
            }
            return true;
        }
        internal bool Matches(string source, TokenSlice other, DomReadWork work)
        {
            work.Step();
            if (Length != other.Length) return false;
            for (var i = 0; i < Length; i++)
            {
                work.Step();
                if (source[_start + i] != source[other._start + i]) return false;
            }
            return true;
        }
    }

    private sealed class SliceComparer(string source, DomReadWork work) : IEqualityComparer<TokenSlice>
    {
        public bool Equals(TokenSlice x, TokenSlice y) => x.Matches(source, y, work);
        public int GetHashCode(TokenSlice value) => unchecked((int) value.Hash);
    }

    /// <summary>
    /// https://infra.spec.whatwg.org/#ordered-set-serializer. <c>DomTokenListMembers.Update</c> writes the
    /// attribute again on the way out, which is §7.1's update steps and is deliberately not conditional; this
    /// write is what keeps the set the next read parses in step with the one a member just changed.
    /// </summary>
    private void Write(List<string> tokens) => _element.SetAttributeNS(null, _attribute, string.Join(" ", tokens));
}
