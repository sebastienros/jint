using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

public abstract class Prototype : ObjectInstance
{
    internal readonly Realm _realm;

    private protected Prototype(Engine engine, Realm realm) : base(engine, type: InternalTypes.Object | InternalTypes.PlainObject)
    {
        _realm = realm;
    }

    /// <summary>
    /// Coerces a built-in's callback argument, raising the TypeError from this prototype's own realm.
    /// Deliberately declared here rather than on <see cref="ObjectInstance"/>: the callee realm is what
    /// the spec attributes such an error to, and only a type that knows its realm can supply it. A
    /// built-in whose receiver is an arbitrary object has no callee realm to reach for and must say
    /// <c>_engine.Realm</c> at the call site instead, so that the gap stays visible.
    /// </summary>
    private protected ICallable GetCallable(JsValue source) => source.GetCallable(_realm);
}
