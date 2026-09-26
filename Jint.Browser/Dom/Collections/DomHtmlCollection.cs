using System.Collections;
using Jint.HtmlParser;

namespace Jint.Browser.Dom.Collections;

/// <summary>A live Browser collection over native element identities.</summary>
internal abstract class DomHtmlCollection<T> : IEnumerable<T> where T : Node
{
    internal abstract int Length { get; }
    public abstract IEnumerator<T> GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
