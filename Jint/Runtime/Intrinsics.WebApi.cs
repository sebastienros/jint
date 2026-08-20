#if NET8_0_OR_GREATER
using Jint.WebApi.Base64;
using Jint.WebApi.Console;
using Jint.WebApi.DomException;
using Jint.WebApi.Encoding;
using Jint.WebApi.StructuredClone;
using Jint.WebApi.Timers;

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
    private TimerFunctions? _timers;
    private TextEncoderConstructor? _textEncoder;
    private TextDecoderConstructor? _textDecoder;
    private Base64Function? _atob;
    private Base64Function? _btoa;
    private StructuredCloneFunction? _structuredClone;

    internal DomExceptionConstructor DomException =>
        _domException ??= new DomExceptionConstructor(_engine, _realm, Function.PrototypeObject, Error.PrototypeObject);

    internal ConsoleInstance Console =>
        _console ??= new ConsoleInstance(_engine, _realm, Object.PrototypeObject);

    /// <summary>
    /// The timer globals, which are five ordinary functions rather than one object — this holder exists only
    /// so that a realm builds one of each, and each of the five is itself built on first access.
    /// </summary>
    internal TimerFunctions Timers =>
        _timers ??= TimerFunctions.Create(_engine, _realm);
    internal TextEncoderConstructor TextEncoder =>
        _textEncoder ??= new TextEncoderConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal TextDecoderConstructor TextDecoder =>
        _textDecoder ??= new TextDecoderConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal Base64Function Atob =>
        _atob ??= new Base64Function(_engine, _realm, Function.PrototypeObject, isAtob: true);

    internal Base64Function Btoa =>
        _btoa ??= new Base64Function(_engine, _realm, Function.PrototypeObject, isAtob: false);

    internal StructuredCloneFunction StructuredClone =>
        _structuredClone ??= new StructuredCloneFunction(_engine, _realm, Function.PrototypeObject);
}
#endif
