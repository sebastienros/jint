using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// Base class for a host-defined constructor — a callable script reaches with <c>new</c>. A subclass supplies
/// <see cref="Construct"/>; calling it without <c>new</c> raises the specification's <c>TypeError</c> unless
/// the subclass overrides <see cref="Call"/> as well. For a callable that is <i>not</i> a constructor, derive
/// from <see cref="HostFunction"/>.
/// </summary>
public abstract class Constructor : Function.Function, IConstructor
{
    /// <summary>
    /// Creates the constructor object against <paramref name="engine"/>'s principal realm.
    /// </summary>
    protected Constructor(Engine engine, string name) : this(engine, engine.Realm, new JsString(name))
    {
        // The engine's ORIGINAL intrinsics, not the running realm's, for the reason ClrFunction and
        // HostFunction share: a constructor a host builds belongs to the realm the surrounding script can
        // reach, whatever realm happened to be current when it was built. Without this the object inherited
        // from Object.prototype — the default every ObjectInstance starts with — so a host constructor was
        // not `instanceof Function` and had no `call`, `apply` or `bind`.
        _prototype = engine._originalIntrinsics.Function.PrototypeObject;
    }

    internal Constructor(Engine engine, Realm realm, JsString name) : base(engine, realm, name)
    {
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        return null;
    }

    public abstract ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget);
}
