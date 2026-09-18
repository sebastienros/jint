using AngleSharp;
using AngleSharp.Dom;
using AngleSharp.Html.Construction;
using AngleSharp.Text;
using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>https://dom.spec.whatwg.org/#dom-domimplementation-createdocumenttype.</summary>
internal static class DomDocumentTypeFactory
{
    internal static JsValue Create(DomRealm realm, IImplementation implementation, JsValue[] arguments)
    {
        const string member = "DOMImplementation.createDocumentType";
        if (arguments.Length < 3)
        {
            Throw.TypeError(realm.OwningRealm, "Failed to execute '" + member + "': 3 arguments required.");
        }

        // WebIDL converts every argument, once and in order, before the DOM algorithm validates the name.
        var name = DomConvert.RequiredText(arguments, 0, member);
        var publicId = DomConvert.RequiredText(arguments, 1, member);
        var systemId = DomConvert.RequiredText(arguments, 2, member);
        if (!DomNames.IsValidDoctypeName(name))
        {
            DomFailures.Refuse(realm, member, DomExceptionNames.InvalidCharacter, "The doctype name is invalid.");
        }

        if (name.IsXmlName() && name.IsQualifiedName())
        {
            return realm.WrapNodeValue(implementation.CreateDocumentType(name, publicId, systemId));
        }

        // IImplementation exposes no owner. A detached native doctype gives us its actual document,
        // including for saved implementations and secondary documents; the realm's document may differ.
        // Only names rejected by the ordinary factory pay for this temporary, never-wrapped node.
        var owner = (Document) implementation.CreateDocumentType("html", "", "").Owner!;
        var factory = owner.Context.GetFactory<IHtmlElementConstructionFactory>();
        var node = (IDocumentType) factory.CreateDocumentType(owner, name, publicId, systemId);
        // This is the parser's native DocumentType, built directly without tokenization, case folding
        // or delimiter escaping. Native insertion, document.doctype, cloning and adoption all see it.
        return realm.WrapNodeValue(node);
    }
}
