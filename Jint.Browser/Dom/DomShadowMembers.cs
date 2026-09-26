using Jint.Browser.CustomElements;
using Jint.HtmlParser;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Dom;

internal static class DomShadowMembers
{
    // DOM §4.9 attachShadow; WebIDL dictionary conversion observes members in lexical order.
    internal static JsValue Attach(DomRealm realm, Element host, JsValue[] arguments)
    {
        const string member = "Element.attachShadow";
        DomConvert.Require(arguments, 0, member);
        var argument = arguments[0];
        if (!argument.IsNullOrUndefined() && argument is not ObjectInstance)
            Throw.TypeError(realm.OwningRealm, "ShadowRootInit must be an object.");
        var dictionary = argument as ObjectInstance;
        var clonable = TypeConverter.ToBoolean(dictionary?.Get("clonable") ?? JsValue.Undefined);
        var registryValue = dictionary?.Get("customElementRegistry") ?? JsValue.Undefined;
        CustomElementRegistryIdentity? registry = null;
        if (!registryValue.IsUndefined())
        {
            if (registryValue.IsNull()) registry = null;
            else if (registryValue is CustomElementRegistry supplied) registry = supplied.Identity;
            else Throw.TypeError(realm.OwningRealm, "customElementRegistry must be a CustomElementRegistry or null.");
        }
        var delegatesFocus = TypeConverter.ToBoolean(dictionary?.Get("delegatesFocus") ?? JsValue.Undefined);
        var modeValue = dictionary?.Get("mode") ?? JsValue.Undefined;
        if (modeValue.IsUndefined()) Throw.TypeError(realm.OwningRealm, "ShadowRootInit.mode is required.");
        var mode = DomEnums.ToShadowRootMode(modeValue, member);
        var serializable = TypeConverter.ToBoolean(dictionary?.Get("serializable") ?? JsValue.Undefined);
        var slotValue = dictionary?.Get("slotAssignment") ?? JsValue.Undefined;
        var slotText = slotValue.IsUndefined() ? "named" : TypeConverter.ToString(slotValue);
        var slot = slotText switch
        {
            "named" => SlotAssignmentMode.Named,
            "manual" => SlotAssignmentMode.Manual,
            _ => DomConvert.BadEnumValue<SlotAssignmentMode>(slotValue, slotText, member),
        };
        // Dictionary getters may adopt the receiver; the algorithm reads its node document afterwards.
        var document = host.OwnerDocument!;
        if (registryValue.IsUndefined()) registry = document.CustomElementRegistry;
        if (registry is { IsScoped: false } && !ReferenceEquals(registry, document.CustomElementRegistry))
            throw DomException.NotSupported();
        var context = DomShadowHostContext.Of(host, CustomElementRegistry.Of(realm.Engine)) with { Registry = registry };
        realm.Engine.Constraints.Check();
        realm.CancellationToken.ThrowIfCancellationRequested();
        var root = ShadowTree.Attach(host, new(mode, delegatesFocus, serializable, slot, clonable), context,
            realm.NativeReadCheckpoint, realm.CancellationToken);
        realm.Engine.Constraints.Check();
        realm.CancellationToken.ThrowIfCancellationRequested();
        return realm.WrapNode(root);
    }
}
