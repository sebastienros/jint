#if NET8_0_OR_GREATER
using Jint.WebApi.Console;
using Jint.WebApi.DomException;

namespace Jint.Runtime;

/// <summary>
/// The opt-in web platform APIs, lazily created per realm exactly like the ECMAScript intrinsics beside them.
/// Requires .NET 8 or higher.
/// </summary>
/// <remarks>
/// Reaching one of these fields is what creates it, and nothing but the global installed by
/// <c>Jint.WebApi.WebApiRegistration</c> reaches them — which is itself a lazy property descriptor. So an
/// engine that enabled a feature whose global the script never mentions has still built nothing, and a
/// <c>ShadowRealm</c>'s intrinsics are simply never asked.
/// </remarks>
public sealed partial class Intrinsics
{
    private DomExceptionConstructor? _domException;
    private ConsoleInstance? _console;

    internal DomExceptionConstructor DomException =>
        _domException ??= new DomExceptionConstructor(_engine, _realm, Function.PrototypeObject, Error.PrototypeObject);

    internal ConsoleInstance Console =>
        _console ??= new ConsoleInstance(_engine, _realm, Object.PrototypeObject);
}
#endif
