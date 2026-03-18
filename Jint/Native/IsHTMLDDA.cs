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
internal sealed class IsHTMLDDA : ObjectInstance, ICallable
{
    internal IsHTMLDDA(Engine engine) : base(engine, ObjectClass.Object, InternalTypes.Object | InternalTypes.IsHTMLDDA)
    {
    }

    JsValue ICallable.Call(JsValue thisObject, JsCallArguments arguments) => Null;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-toboolean
    /// Step 1: If argument has an [[IsHTMLDDA]] internal slot, return false.
    /// </summary>
    internal override bool ToBoolean() => false;

    protected internal override bool IsLooselyEqual(JsValue value)
    {
        if (value.IsNull() || value.IsUndefined())
        {
            return true;
        }

        return base.IsLooselyEqual(value);
    }
}
