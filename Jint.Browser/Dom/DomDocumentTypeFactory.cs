using Jint.Native;
using Jint.Runtime;
using Jint.WebApi.DomException;

namespace Jint.Browser.Dom;

/// <summary>https://dom.spec.whatwg.org/#dom-domimplementation-createdocumenttype.</summary>
internal static class DomDocumentTypeFactory
{
    internal static JsValue Create(DomRealm realm, DomImplementation implementation, JsValue[] arguments)
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

        return realm.WrapNodeValue(implementation.Document.CreateDocumentType(name, publicId, systemId));
    }
}
