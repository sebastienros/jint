#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.Workers;

/// <summary>
/// <c>Worker.prototype</c> — the interface prototype object.
/// <para>
/// https://html.spec.whatwg.org/multipage/workers.html#dedicated-workers-and-the-worker-interface
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>EventTarget.prototype</c>, so a worker has <c>addEventListener</c> and the
/// rest and <c>worker instanceof EventTarget</c> holds. The three event-handler attributes are the ones the
/// IDL declares — <c>onmessage</c> and <c>onmessageerror</c> from <c>MessageEventTarget</c>, <c>onerror</c>
/// from <c>AbstractWorker</c>; <c>messageerror</c> exists and can never fire, for the reason
/// <c>JsMessagePort</c> records.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class WorkerPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly WorkerConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString WorkerToStringTag = new("Worker");

    internal WorkerPrototype(
        Engine engine,
        Realm realm,
        WorkerConstructor constructor,
        ObjectInstance eventTargetPrototype) : base(engine, realm)
    {
        _prototype = eventTargetPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/workers.html#dom-worker-postmessage — "the <i>message port post
    /// message steps</i> ... with the worker's port".
    /// </summary>
    /// <remarks>
    /// Two overloads share one name, and WebIDL's overload resolution picks between them by asking whether the
    /// second argument is iterable (https://webidl.spec.whatwg.org/#es-overloads step 12.3), exactly as
    /// <c>MessagePort.postMessage</c> does — so both <c>postMessage(x, [buf])</c> and
    /// <c>postMessage(x, { transfer: [buf] })</c> work, and the list travels verbatim, which is what lets a
    /// <c>MessagePort</c> and a transferable stream ride through untouched.
    /// </remarks>
    [JsFunction(Name = "postMessage", Length = 1)]
    private JsValue PostMessage(JsValue thisObject, JsCallArguments arguments)
    {
        var worker = Brand(thisObject);

        if (arguments.Length == 0)
        {
            Throw.TypeError(_realm, "Failed to execute 'postMessage' on 'Worker': 1 argument required, but only 0 present.");
        }

        var transferList = ReadTransferArgument(arguments.At(1));
        worker.Link?.PostFromParent(_realm, arguments[0], transferList);
        return Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/workers.html#dom-worker-terminate — <i>terminate a worker</i>.
    /// </summary>
    /// <remarks>
    /// Idempotent, and an immediate parent→worker fence the instant it returns: both endpoints are closed, so
    /// nothing can be enqueued in either direction any more. Stopping the worker's <i>script</i> is
    /// cooperative — the cancellation the token carries is observed within the engine's amortized check
    /// interval on the worker's own thread, and not at all while it is inside a host CLR call. That bound is
    /// stronger than the field's: V8's <c>TerminateExecution</c> is frame-bounded in exactly the same way and
    /// does not unwind an embedder's native call either.
    /// </remarks>
    [JsFunction(Name = "terminate", Length = 0)]
    private JsValue Terminate(JsValue thisObject)
    {
        Brand(thisObject).Link?.Terminate();
        return Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/workers.html#handler-worker-onmessage.
    /// </summary>
    /// <remarks>
    /// The handler is one entry of this object's own event listener list, so it takes its turn in registration
    /// order among the <c>addEventListener('message', …)</c> listeners. <b>Assigning it starts nothing</b> —
    /// the parent's queue is enabled by the engine when the worker is created, so a listener registered either
    /// way receives; see <c>WorkerErrors.SetEventHandler</c>.
    /// </remarks>
    [JsAccessor("onmessage", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageGet(JsValue thisObject)
        => WorkerErrors.GetEventHandler(Brand(thisObject), JsWorker.MessageEventType);

    /// <inheritdoc cref="OnMessageGet" />
    [JsAccessor("onmessage", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageSet(JsValue thisObject, JsValue value)
    {
        WorkerErrors.SetEventHandler(Brand(thisObject), JsWorker.MessageEventType, value);
        return Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/workers.html#handler-worker-onmessageerror. Present because the
    /// interface has it; nothing in Jint ever fires one.
    /// </summary>
    [JsAccessor("onmessageerror", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageErrorGet(JsValue thisObject)
        => WorkerErrors.GetEventHandler(Brand(thisObject), JsWorker.MessageErrorEventType);

    /// <inheritdoc cref="OnMessageErrorGet" />
    [JsAccessor("onmessageerror", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnMessageErrorSet(JsValue thisObject, JsValue value)
    {
        WorkerErrors.SetEventHandler(Brand(thisObject), JsWorker.MessageErrorEventType, value);
        return Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/workers.html#handler-abstractworker-onerror — the <i>plain</i>
    /// <c>EventHandler</c> shape: invoked with the event, cancelled with <c>preventDefault()</c>. (The worker
    /// global's <c>onerror</c> is the legacy five-argument one instead.)
    /// </summary>
    /// <remarks>
    /// The attribute exists from this change; what fires at it — a plain <c>Event</c> for a load failure and
    /// an <c>ErrorEvent</c> with <c>error: null</c> for a runtime error — lands with the parent-side error
    /// channels in their own change. Until then a load failure is reported to the <i>host</i> instead, through
    /// <see cref="WorkerConnection.IsFaulted"/> and <see cref="WorkerConnection.Error"/>.
    /// </remarks>
    [JsAccessor("onerror", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorGet(JsValue thisObject)
        => WorkerErrors.GetEventHandler(Brand(thisObject), JsWorker.ErrorEventType);

    /// <inheritdoc cref="OnErrorGet" />
    [JsAccessor("onerror", AccessorKind.Set, Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsValue OnErrorSet(JsValue thisObject, JsValue value)
    {
        WorkerErrors.SetEventHandler(Brand(thisObject), JsWorker.ErrorEventType, value);
        return Undefined;
    }

    /// <summary>
    /// Resolves <c>postMessage</c>'s second argument to a transfer list. See the operation's remarks for the
    /// overload rule.
    /// </summary>
    private List<JsValue>? ReadTransferArgument(JsValue argument)
    {
        const string Operation = "postMessage' on 'Worker";

        if (argument is ObjectInstance && JsValue.GetMethod(_realm, argument, GlobalSymbolRegistry.Iterator) is not null)
        {
            return StructuredSerializeOptions.ReadTransferSequence(_realm, argument, Operation);
        }

        return StructuredSerializeOptions.ReadTransferOption(_realm, argument, Operation);
    }

    private JsWorker Brand(JsValue thisObject)
    {
        if (thisObject is JsWorker worker)
        {
            return worker;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a Worker");
        return null!;
    }
}
#endif
