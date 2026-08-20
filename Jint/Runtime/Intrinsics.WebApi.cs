#if NET8_0_OR_GREATER
using Jint.WebApi.Abort;
using Jint.WebApi.Base64;
using Jint.WebApi.Console;
using Jint.WebApi.DomException;
using Jint.WebApi.Encoding;
using Jint.WebApi.Events;
using Jint.WebApi.Files;
using Jint.WebApi.StructuredClone;
using Jint.WebApi.Timers;
using Jint.WebApi.Url;

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
    private UrlConstructor? _webApiUrl;
    private UrlSearchParamsConstructor? _webApiUrlSearchParams;

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
    private BlobConstructor? _blob;
    private FileConstructor? _file;
    private FormDataConstructor? _formData;
    private FormDataIteratorPrototype? _formDataIteratorPrototype;

    internal BlobConstructor Blob =>
        _blob ??= new BlobConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal FileConstructor File =>
        _file ??= new FileConstructor(_engine, _realm, Blob);

    internal FormDataConstructor FormData =>
        _formData ??= new FormDataConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal FormDataIteratorPrototype FormDataIteratorPrototype =>
        _formDataIteratorPrototype ??= new FormDataIteratorPrototype(_engine, _realm, IteratorPrototype);

    internal UrlConstructor WebApiUrl =>
        _webApiUrl ??= new UrlConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal UrlSearchParamsConstructor WebApiUrlSearchParams =>
        _webApiUrlSearchParams ??= new UrlSearchParamsConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    private EventConstructor? _event;
    private CustomEventConstructor? _customEvent;
    private EventTargetConstructor? _eventTarget;
    private AbortSignalConstructor? _abortSignal;
    private AbortControllerConstructor? _abortController;

    internal EventConstructor Event =>
        _event ??= new EventConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    /// <summary>
    /// <c>CustomEvent</c> inherits from <c>Event</c>, so reaching it builds <c>Event</c> too — which is what
    /// makes <c>Object.getPrototypeOf(CustomEvent) === Event</c> hold however the two were first touched.
    /// </summary>
    internal CustomEventConstructor CustomEvent =>
        _customEvent ??= new CustomEventConstructor(_engine, _realm, Event);

    internal EventTargetConstructor EventTarget =>
        _eventTarget ??= new EventTargetConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    /// <summary><c>AbortSignal</c> inherits from <c>EventTarget</c>.</summary>
    internal AbortSignalConstructor AbortSignal =>
        _abortSignal ??= new AbortSignalConstructor(_engine, _realm, EventTarget);

    internal AbortControllerConstructor AbortController =>
        _abortController ??= new AbortControllerConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject, AbortSignal);
}
#endif
