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
    /// When this error was produced by a failed CLR interop method or constructor resolution, the
    /// originating CLR type. These are plain CLR fields (not JavaScript properties), so a running script
    /// cannot observe them; the host reads them via <see cref="JintException.TryGetClrType"/>.
    /// </summary>
    internal Type? ClrResolutionType { get; private set; }

    internal string? ClrResolutionMemberName { get; private set; }

    internal void SetClrResolutionInfo(Type clrType, string? memberName)
    {
        ClrResolutionType = clrType;
        ClrResolutionMemberName = memberName;
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
        return Engine.Realm.Intrinsics.Error.PrototypeObject.ToString(this).ToObject()?.ToString() ?? "";
    }
}
