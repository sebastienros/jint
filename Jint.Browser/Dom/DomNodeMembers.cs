using AngleSharp.Dom;
using Jint.Native;
using Jint.Runtime;

namespace Jint.Browser.Dom;

/// <summary>
/// The <c>Node</c> members AngleSharp has no <c>[DomName]</c> for, so nothing could generate them.
/// </summary>
/// <remarks>
/// One so far. <c>getRootNode()</c> is DOM §4.4's, and it is not an exotic corner: it is how a library asks
/// "am I in the document, or in a shadow tree, or in a fragment nobody has inserted yet" — Playwright's
/// injected script calls it on every element it touches, which is where its absence was found.
/// </remarks>
internal static class DomNodeMembers
{
    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-node-getrootnode — the topmost inclusive ancestor, crossing a shadow
    /// boundary only when the caller asked for the composed root.
    /// </summary>
    internal static JsValue GetRootNode(DomRealm realm, INode target, JsValue[] arguments)
    {
        var composed = TypeConverter.ToBoolean(DomConvert.DictionaryMember(arguments, 0, "composed"));
        var current = target;

        while (true)
        {
            if (current.Parent is { } parent)
            {
                current = parent;
                continue;
            }

            // A shadow root's parent is null, so the walk stops there — which is the whole of what "root"
            // means without `composed`. With it, the host's tree is the one that continues.
            if (composed && current is IShadowRoot shadow && shadow.Host is { } host)
            {
                current = host;
                continue;
            }

            return realm.WrapNodeValue(current);
        }
    }
}
