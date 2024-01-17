using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Error;

public class ErrorInstance : ObjectInstance
{
    private protected ErrorInstance(Engine engine, ObjectClass objectClass)
        : base(engine, objectClass)
    {
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-installerrorcause
    /// </summary>
    internal void InstallErrorCause(JsValue options)
    {
        if (options is ObjectInstance oi && oi.HasProperty("cause"))
        {
            var cause = oi.Get("cause");
            CreateNonEnumerableDataPropertyOrThrow("cause", cause);
        }
    }

    public override string ToString()
    {
        return Engine.Realm.Intrinsics.Error.PrototypeObject.ToString(this, Arguments.Empty).ToObject()?.ToString() ?? "";
    }
}
