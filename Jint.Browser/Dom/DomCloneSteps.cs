using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Jint.Browser.Dom;

internal static class DomCloneSteps
{
    /// <summary>https://dom.spec.whatwg.org/#concept-node-clone: preserve metadata and native data throughout a copied subtree.</summary>
    internal static void Copy(INode source, INode copy)
    {
        var pending = new Stack<(INode Source, INode Copy)>();
        pending.Push((source, copy));
        while (pending.TryPop(out var pair))
        {
            if (pair.Source is IElement original && pair.Copy is IElement cloned
                && string.IsNullOrEmpty(cloned.GivenNamespaceUri))
            {
                DomNamespaces.Created(cloned, DomNamespaces.Of(original));
            }
            else if (pair.Source is IProcessingInstruction instruction && pair.Copy is IProcessingInstruction instructionCopy)
            {
                // Native PI cloning currently omits data. A new identity parses this copied data
                // lazily; source attribute state is not copied (it can contain non-XML names).
                instructionCopy.Data = instruction.Data;
            }
            var sources = pair.Source.ChildNodes;
            var copies = pair.Copy.ChildNodes;
            for (var i = 0; i < Math.Min(sources.Length, copies.Length); i++)
            {
                pending.Push((sources[i], copies[i]));
            }
            if (pair.Source is IHtmlTemplateElement template && pair.Copy is IHtmlTemplateElement templateCopy)
            {
                pending.Push((template.Content, templateCopy.Content));
            }
        }
    }
}
