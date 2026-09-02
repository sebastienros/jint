using System.Text;
using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.DevTools.Domains;
using Jint.Native;

namespace Jint.Browser.DevTools;

/// <summary>
/// What a node looks like to a client: <c>subtype: "node"</c>, the interface it implements, and Chrome's
/// one-line description.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the seam <c>Jint.DevTools</c> left open.</b> That package knows the language's values and
/// nothing else, so a DOM node reaches a client as an ordinary object with no subtype and a class name of
/// <c>Object</c>; the protocol has a vocabulary for one, and only the package that owns the DOM can fill it
/// in. Every client's element handle depends on it: PuppeteerSharp's <c>ElementHandle</c> is what a
/// <c>RemoteObject</c> with <c>subtype: "node"</c> becomes, and a plain <c>JSHandle</c> is what one without
/// it becomes — so <c>$('button').click()</c> fails on the missing subtype long before it reaches
/// <c>Input.dispatchMouseEvent</c>.
/// </para>
/// <para>
/// <b>It runs nothing.</b> The description is read from the node's own name, its <c>id</c> and <c>class</c>
/// content attributes and its interface — all of them CLR reads on AngleSharp's tree, none of them a
/// script-visible accessor. That is the promise every describing path in the protocol keeps, and a describer
/// is held to it exactly as the engine's own inspector is.
/// </para>
/// <para>
/// The class name is the DOM interface the wrapper was given — <c>HTMLDivElement</c>, <c>Text</c>,
/// <c>HTMLDocument</c> — which is the same string <c>Object.prototype.toString</c> reports for it, because
/// both come from <c>DomInterfaceDefinition.Name</c>.
/// </para>
/// </remarks>
internal sealed class DomRemoteObjectDescriber : RemoteObjectDescriber
{
    /// <summary>The one instance: it holds nothing, and every page target uses it.</summary>
    internal static DomRemoteObjectDescriber Instance { get; } = new();

    private DomRemoteObjectDescriber()
    {
    }

    /// <inheritdoc/>
    internal override bool TryDescribe(JsValue value, out RemoteObjectHint hint)
    {
        if (value is not DomNodeObject wrapper)
        {
            hint = default;
            return false;
        }

        hint = new RemoteObjectHint
        {
            Subtype = "node",
            ClassName = wrapper.Definition.Name,
            Description = Describe(wrapper.Node),
        };

        return true;
    }

    /// <summary>
    /// Chrome's own one-line form: <c>div#id.one.two</c> for an element, and the node name for everything
    /// else — <c>#text</c>, <c>#comment</c>, <c>#document</c>, <c>#document-fragment</c>.
    /// </summary>
    /// <remarks>
    /// The tag name is lower-cased for an HTML element and left alone otherwise, which is what makes an SVG
    /// <c>clipPath</c> read as itself; AngleSharp's <c>LocalName</c> already answers that distinction.
    /// </remarks>
    internal static string Describe(INode node)
    {
        if (node is not IElement element)
        {
            return node.NodeName;
        }

        var builder = new StringBuilder(element.LocalName);

        if (element.Id is { Length: > 0 } id)
        {
            builder.Append('#').Append(id);
        }

        foreach (var name in element.ClassList)
        {
            if (name.Length != 0)
            {
                builder.Append('.').Append(name);
            }
        }

        return builder.ToString();
    }
}
