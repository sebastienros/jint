using System.Collections;
using System.Diagnostics.CodeAnalysis;
using AngleSharp.Dom;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// An <see cref="IHtmlCollection{T}"/> whose filter is evaluated against the current tree for every read.
/// </summary>
/// <remarks>
/// <para>
/// DOM collections are live unless their defining algorithm says otherwise. AngleSharp's tag-name and
/// class-name queries materialize a snapshot, so the binding supplies this small adapter for those
/// operations. Keeping the target as an <see cref="IHtmlCollection{T}"/> lets the ordinary HTMLCollection
/// wrapper retain the one indexed and named-property implementation used by every other collection.
/// </para>
/// <para>
/// <b>Every read is one walk, and it allocates nothing.</b> The element at an index is found by walking until
/// the walk has passed that many matches — so running out <i>is</i> the bounds answer and no separate
/// <see cref="Length"/> probe precedes it — and the walk itself is <see cref="DomElementWalker"/>, whose
/// remarks carry the measurement that motivated both. Nothing here memoizes: these collections are live by
/// specification, so a second read re-runs the filter against whatever the tree now is.
/// </para>
/// <para>
/// <b>Two sources.</b> Nearly every live collection in the surface is "the element descendants of a root that
/// match a filter", which is the <see cref="DomElementWalker"/> form. The exception is <c>document.all</c>'s
/// named sub-collection, whose source is another live collection rather than a tree and which therefore
/// filters a sequence; it is reached only from a named read of <c>document.all</c> that several elements
/// match, and is deliberately left on that shape rather than given a wrapper of its own.
/// </para>
/// </remarks>
internal sealed class DomLiveHtmlCollection : IHtmlCollection<IElement>
{
    private readonly INode? _root;
    private readonly IEnumerable<IElement>? _source;
    private readonly DomElementFilter _filter;

    /// <summary>The element descendants of <paramref name="root"/> that <paramref name="filter"/> matches.</summary>
    internal DomLiveHtmlCollection(INode root, DomElementFilter filter)
    {
        _root = root;
        _filter = filter;
    }

    /// <summary>The members of <paramref name="source"/> that <paramref name="filter"/> matches.</summary>
    internal DomLiveHtmlCollection(IEnumerable<IElement> source, DomElementFilter filter)
    {
        _source = source;
        _filter = filter;
    }

    public int Length
    {
        get
        {
            if (_filter.MatchesNothing)
            {
                return 0;
            }

            var count = 0;

            if (_root is { } root)
            {
                _filter.BeginRead();
                var walker = new DomElementWalker(root);
                while (walker.MoveNext())
                {
                    if (_filter.Matches(walker.Current))
                    {
                        count++;
                    }
                }

                return count;
            }

            foreach (var unused in Filtered())
            {
                count++;
            }

            return count;
        }
    }

    public int Count => Length;

    /// <summary>
    /// The element at <paramref name="index"/>, and <see langword="false"/> when the collection has none —
    /// which is the authoritative bounds answer, taken from the one walk rather than from a
    /// <see cref="Length"/> that would have had to run the whole query again.
    /// </summary>
    internal bool TryGetElementAt(uint index, [NotNullWhen(true)] out IElement? element)
    {
        if (!_filter.MatchesNothing)
        {
            var remaining = index;

            if (_root is { } root)
            {
                _filter.BeginRead();
                var walker = new DomElementWalker(root);
                while (walker.MoveNext())
                {
                    var candidate = walker.Current;
                    if (!_filter.Matches(candidate))
                    {
                        continue;
                    }

                    if (remaining == 0)
                    {
                        element = candidate;
                        return true;
                    }

                    remaining--;
                }
            }
            else
            {
                foreach (var candidate in Filtered())
                {
                    if (remaining == 0)
                    {
                        element = candidate;
                        return true;
                    }

                    remaining--;
                }
            }
        }

        element = null;
        return false;
    }

    public IElement this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);

            if (!TryGetElementAt((uint) index, out var element))
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return element;
        }
    }

    /// <summary>
    /// HTML's named lookup: the <b>first</b> element in tree order whose ID is <paramref name="id"/>, or which
    /// is an HTML element whose <c>name</c> content attribute is. One pass, and the same rule
    /// <c>DomHtmlCollectionObject.NamedItem</c> applies to the wrapper — which is the one script reaches.
    /// </summary>
    public IElement? this[string id]
    {
        get
        {
            if (_filter.MatchesNothing)
            {
                return null;
            }

            if (_root is { } root)
            {
                _filter.BeginRead();
                var walker = new DomElementWalker(root);
                while (walker.MoveNext())
                {
                    var candidate = walker.Current;
                    if (_filter.Matches(candidate) && IsNamed(candidate, id))
                    {
                        return candidate;
                    }
                }

                return null;
            }

            foreach (var candidate in Filtered())
            {
                if (IsNamed(candidate, id))
                {
                    return candidate;
                }
            }

            return null;
        }
    }

    private static bool IsNamed(IElement element, string id)
        => string.Equals(element.Id, id, StringComparison.Ordinal)
           || (element is AngleSharp.Html.Dom.IHtmlElement
               && string.Equals(element.GetAttribute("name"), id, StringComparison.Ordinal));

    public IEnumerator<IElement> GetEnumerator() => Filtered().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// The matching elements as a sequence, for the callers that really do visit all of them — the named
    /// half of <c>HTMLCollection</c>, and the sequence-sourced form. The indexed lane does not come through
    /// here, because a state machine is an allocation and it reads one element.
    /// </summary>
    private IEnumerable<IElement> Filtered()
    {
        if (_filter.MatchesNothing)
        {
            yield break;
        }

        _filter.BeginRead();

        if (_root is { } root)
        {
            var walker = new DomElementWalker(root);
            while (walker.MoveNext())
            {
                var candidate = walker.Current;
                if (_filter.Matches(candidate))
                {
                    yield return candidate;
                }
            }

            yield break;
        }

        foreach (var candidate in _source!)
        {
            if (_filter.Matches(candidate))
            {
                yield return candidate;
            }
        }
    }
}
