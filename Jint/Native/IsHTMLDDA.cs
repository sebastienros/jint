using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// https://tc39.es/ecma262/#sec-IsHTMLDDA-internal-slot
/// An object with the [[IsHTMLDDA]] internal slot has special behavior for:
/// - typeof returns "undefined"
/// - loose equality with null/undefined returns true
/// - calling it returns null (per test262 $262.IsHTMLDDA contract)
/// </summary>
/// <remarks>
/// The three Annex B behaviours belong to the slot rather than to this class:
/// <see cref="ObjectInstance.DeclareIsHtmlDda"/> and the flag it sets implement them once, for every bearer.
/// What is this class's own is the [[Call]] test262's contract gives it. <c>Jint.Browser</c>'s
/// <c>HTMLAllCollection</c> is the other bearer, and its [[Call]] is HTML's legacy caller instead.
/// </remarks>
internal sealed class IsHTMLDDA : ObjectInstance, ICallable
{
    internal IsHTMLDDA(Engine engine, Realm realm) : base(engine, ObjectClass.Object, InternalTypes.Object | InternalTypes.IsHTMLDDA | InternalTypes.Callable)
    {
        // The base constructor takes the prototype from whichever realm is active; this object may be
        // built for another one ($262.createRealm() installs a $262 into the realm it just made).
        _prototype = realm.Intrinsics.Object.PrototypeObject;
    }

    JsValue ICallable.Call(JsValue thisObject, JsCallArguments arguments) => Null;
}
