#if NET8_0_OR_GREATER
using Jint.WebApi.Abort;
using Jint.WebApi.Base64;
using Jint.WebApi.Console;
using Jint.WebApi.Crypto;
using Jint.WebApi.DomException;
using Jint.WebApi.Encoding;
using Jint.WebApi.Events;
using Jint.WebApi.Fetch;
using Jint.WebApi.Files;
using Jint.WebApi.Navigator;
using Jint.WebApi.StructuredClone;
using Jint.WebApi.Performance;
using Jint.WebApi.Scheduling;
using Jint.WebApi.Streams;
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
    private CryptoInstance? _crypto;
    private SubtleCryptoInstance? _subtleCrypto;
    private PerformanceInstance? _performance;
    private PerformanceEntryConstructor? _performanceEntry;
    private PerformanceMarkConstructor? _performanceMark;
    private PerformanceMeasureConstructor? _performanceMeasure;
    private NavigatorInstance? _navigator;

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
    private HeadersConstructor? _headers;
    private RequestConstructor? _request;
    private ResponseConstructor? _response;
    private FetchFunction? _fetch;

    /// <summary>
    /// The global <c>fetch</c> function — one per realm, built on first access like every intrinsic beside it.
    /// </summary>
    internal FetchFunction Fetch =>
        _fetch ??= FetchFunction.Create(_engine, _realm, Function.PrototypeObject);

    internal HeadersConstructor Headers =>
        _headers ??= new HeadersConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal RequestConstructor Request =>
        _request ??= new RequestConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal ResponseConstructor Response =>
        _response ??= new ResponseConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal CryptoInstance Crypto =>
        _crypto ??= new CryptoInstance(_engine, _realm, Object.PrototypeObject);

    /// <summary>
    /// The object <c>crypto.subtle</c> answers with. It has no global of its own — the only way to reach it
    /// is through the <c>crypto</c> object, whose accessor returns this one every time, which is what makes
    /// <c>crypto.subtle === crypto.subtle</c> hold.
    /// </summary>
    internal SubtleCryptoInstance SubtleCrypto =>
        _subtleCrypto ??= new SubtleCryptoInstance(_engine, _realm, Object.PrototypeObject);

    /// <summary>
    /// The <c>performance</c> object, which reads the engine's time origin and therefore needs the web-API
    /// state <c>WebApiRegistration</c> created alongside the global.
    /// </summary>
    internal PerformanceInstance Performance =>
        _performance ??= PerformanceInstance.Create(_engine, _realm, Object.PrototypeObject);

    internal PerformanceEntryConstructor PerformanceEntry =>
        _performanceEntry ??= new PerformanceEntryConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    /// <summary>
    /// <c>PerformanceMark</c> inherits from <c>PerformanceEntry</c>, so reaching it builds
    /// <c>PerformanceEntry</c> too — which is what makes
    /// <c>Object.getPrototypeOf(PerformanceMark) === PerformanceEntry</c> hold however the two were first
    /// touched.
    /// </summary>
    internal PerformanceMarkConstructor PerformanceMark =>
        _performanceMark ??= new PerformanceMarkConstructor(_engine, _realm, PerformanceEntry);

    /// <summary><c>PerformanceMeasure</c> inherits from <c>PerformanceEntry</c>.</summary>
    internal PerformanceMeasureConstructor PerformanceMeasure =>
        _performanceMeasure ??= new PerformanceMeasureConstructor(_engine, _realm, PerformanceEntry);

    internal NavigatorInstance Navigator =>
        _navigator ??= new NavigatorInstance(_engine, _realm, Object.PrototypeObject);

    private ReadableStreamConstructor? _readableStream;
    private ReadableStreamDefaultReaderConstructor? _readableStreamDefaultReader;
    private ReadableStreamDefaultControllerConstructor? _readableStreamDefaultController;
    private ReadableStreamAsyncIteratorPrototype? _readableStreamAsyncIteratorPrototype;
    private WritableStreamConstructor? _writableStream;
    private WritableStreamDefaultWriterConstructor? _writableStreamDefaultWriter;
    private WritableStreamDefaultControllerConstructor? _writableStreamDefaultController;
    private TransformStreamConstructor? _transformStream;
    private TransformStreamDefaultControllerConstructor? _transformStreamDefaultController;
    private CountQueuingStrategyConstructor? _countQueuingStrategy;
    private ByteLengthQueuingStrategyConstructor? _byteLengthQueuingStrategy;

    internal ReadableStreamConstructor ReadableStream =>
        _readableStream ??= new ReadableStreamConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    /// <summary>
    /// <c>ReadableStreamDefaultReader</c>, <c>ReadableStreamDefaultController</c> and the writable and
    /// transform interfaces beside them are deliberately <b>not</b> installed as globals — a browser exposes
    /// every one of them, and Jint exposes only the five a script constructs directly. They are still
    /// ordinary interface objects reachable as <c>Object.getPrototypeOf(reader).constructor</c>, which is
    /// where an <c>instanceof</c> check or a prototype patch goes.
    /// </summary>
    internal ReadableStreamDefaultReaderConstructor ReadableStreamDefaultReader =>
        _readableStreamDefaultReader ??= new ReadableStreamDefaultReaderConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal ReadableStreamDefaultControllerConstructor ReadableStreamDefaultController =>
        _readableStreamDefaultController ??= new ReadableStreamDefaultControllerConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    /// <summary>
    /// <c>%ReadableStreamAsyncIteratorPrototype%</c>, whose <c>[[Prototype]]</c> is
    /// <c>%AsyncIteratorPrototype%</c> — which is what gives an iterator obtained from a stream the async
    /// iterator helpers.
    /// </summary>
    internal ReadableStreamAsyncIteratorPrototype ReadableStreamAsyncIteratorPrototype =>
        _readableStreamAsyncIteratorPrototype ??= new ReadableStreamAsyncIteratorPrototype(_engine, _realm, AsyncIteratorPrototype);

    internal WritableStreamConstructor WritableStream =>
        _writableStream ??= new WritableStreamConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal WritableStreamDefaultWriterConstructor WritableStreamDefaultWriter =>
        _writableStreamDefaultWriter ??= new WritableStreamDefaultWriterConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal WritableStreamDefaultControllerConstructor WritableStreamDefaultController =>
        _writableStreamDefaultController ??= new WritableStreamDefaultControllerConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal TransformStreamConstructor TransformStream =>
        _transformStream ??= new TransformStreamConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal TransformStreamDefaultControllerConstructor TransformStreamDefaultController =>
        _transformStreamDefaultController ??= new TransformStreamDefaultControllerConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal CountQueuingStrategyConstructor CountQueuingStrategy =>
        _countQueuingStrategy ??= new CountQueuingStrategyConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    internal ByteLengthQueuingStrategyConstructor ByteLengthQueuingStrategy =>
        _byteLengthQueuingStrategy ??= new ByteLengthQueuingStrategyConstructor(_engine, _realm, Function.PrototypeObject, Object.PrototypeObject);

    private SchedulerInstance? _scheduler;
    private TaskSignalConstructor? _taskSignal;
    private TaskControllerConstructor? _taskController;
    private TaskPriorityChangeEventConstructor? _taskPriorityChangeEvent;

    /// <summary>
    /// The <c>scheduler</c> object, which reaches the engine's prioritized task queues and the timer queue a
    /// delayed <c>postTask</c> waits on — both created by <c>WebApiRegistration</c> alongside the global.
    /// </summary>
    internal SchedulerInstance Scheduler =>
        _scheduler ??= SchedulerInstance.Create(_engine, _realm, Object.PrototypeObject);

    /// <summary>
    /// <c>TaskSignal</c> inherits from <c>AbortSignal</c>, so reaching it builds <c>AbortSignal</c> — and
    /// therefore <c>EventTarget</c> — too, whether or not the events feature installed their globals.
    /// </summary>
    internal TaskSignalConstructor TaskSignal =>
        _taskSignal ??= new TaskSignalConstructor(_engine, _realm, AbortSignal);

    /// <summary><c>TaskController</c> inherits from <c>AbortController</c>.</summary>
    internal TaskControllerConstructor TaskController =>
        _taskController ??= new TaskControllerConstructor(_engine, _realm, AbortController, TaskSignal);

    /// <summary><c>TaskPriorityChangeEvent</c> inherits from <c>Event</c>.</summary>
    internal TaskPriorityChangeEventConstructor TaskPriorityChangeEvent =>
        _taskPriorityChangeEvent ??= new TaskPriorityChangeEventConstructor(_engine, _realm, Event);
}
#endif
