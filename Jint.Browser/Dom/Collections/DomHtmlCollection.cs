using System.Collections;
using Jint.HtmlParser;
using Jint.Native;

namespace Jint.Browser.Dom.Collections;

/// <summary>A live Browser collection over native element identities.</summary>
internal abstract class DomHtmlCollection<T> : IEnumerable<T> where T : Node
{
    internal abstract int Length { get; }
    internal virtual T? GetItem(uint index)
    {
        foreach (var candidate in this)
        {
            if (index == 0) return candidate;
            index--;
        }
        return null;
    }
    // C# null requests the generic HTMLCollection lookup; JsValue.Null is a specialized miss.
    internal virtual JsValue? GetNamedItem(DomRealm realm, string name) => null;
    internal virtual int GetLength(DomRealm realm)
    {
        realm.Engine.Constraints.Check();
        var length = Length;
        realm.Engine.Constraints.Check();
        return length;
    }
    internal virtual T? GetItem(DomRealm realm, uint index)
    {
        realm.Engine.Constraints.Check();
        var item = GetItem(index);
        realm.Engine.Constraints.Check();
        return item;
    }
    internal virtual IEnumerable<T> Read(DomRealm realm)
    {
        var work = new DomReadWork(realm.NativeReadCheckpoint, realm.CancellationToken);
        work.Check();
        foreach (var item in this) { work.Step(); yield return item; }
        work.Check();
    }
    public abstract IEnumerator<T> GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
