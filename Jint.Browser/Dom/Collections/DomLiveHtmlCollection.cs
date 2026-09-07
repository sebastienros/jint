using System.Collections;
using AngleSharp.Dom;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// An <see cref="IHtmlCollection{T}"/> whose filter is evaluated against the current tree for every read.
/// </summary>
/// <remarks>
/// DOM collections are live unless their defining algorithm says otherwise. AngleSharp's tag-name queries
/// materialize a snapshot, so the binding supplies this small adapter for those four operations. Keeping the
/// target as an <see cref="IHtmlCollection{T}"/> lets the ordinary HTMLCollection wrapper retain the one
/// indexed and named-property implementation used by every other collection.
/// </remarks>
internal sealed class DomLiveHtmlCollection(Func<IEnumerable<IElement>> current) : IHtmlCollection<IElement>
{
    public int Length => current().Count();

    public int Count => Length;

    public IElement this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);

            foreach (var element in Current())
            {
                if (index-- == 0)
                {
                    return element;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }
    }

    public IElement? this[string id]
    {
        get
        {
            foreach (var element in Current())
            {
                if (string.Equals(element.Id, id, StringComparison.Ordinal))
                {
                    return element;
                }
            }

            foreach (var element in Current())
            {
                if (element is AngleSharp.Html.Dom.IHtmlElement
                    && string.Equals(element.GetAttribute("name"), id, StringComparison.Ordinal))
                {
                    return element;
                }
            }

            return null;
        }
    }

    public IEnumerator<IElement> GetEnumerator() => Current().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private IEnumerable<IElement> Current() => current();
}
