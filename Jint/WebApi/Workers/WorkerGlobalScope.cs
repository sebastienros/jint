#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.GlobalEvents;
using Jint.WebApi.Messaging;
using Jint.WebApi.StructuredClone;

namespace Jint.WebApi.Workers;

/// <summary>
/// The names a dedicated worker's global scope carries, installed on the worker engine's global object when
/// the connection is made.
/// <para>
/// https://html.spec.whatwg.org/multipage/workers.html#dedicated-workers-and-the-dedicatedworkerglobalscope-interface
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <b>The two interface objects are real, and so is the prototype chain behind them.</b> The worker engine's
/// global object has <c>DedicatedWorkerGlobalScope.prototype</c> as its <c>[[Prototype]]</c>, which has
/// <c>WorkerGlobalScope.prototype</c> as its own, so
/// <c>'DedicatedWorkerGlobalScope' in self &amp;&amp; self instanceof DedicatedWorkerGlobalScope</c> — the
/// canonical worker feature-detect, and the guard every fixture of web-platform-tests' <c>workers/modules/</c>
/// corpus registers its <c>onmessage</c> inside — takes the branch it takes in a browser. Nothing is shimmed:
/// there is no <c>Symbol.hasInstance</c> anywhere near this, and <c>Object.getPrototypeOf(self)</c> answers
/// the very object <c>instanceof</c> walks to.
/// </para>
/// <para>
/// The chain stops one link early. HTML has <c>WorkerGlobalScope : EventTarget</c>, and Jint's global object
/// is not an <c>EventTarget</c> — <c>addEventListener</c> is an ordinary function on the global bound to the
/// synthetic listener list <see cref="GlobalEventTarget"/> keeps, which is the "suitable alternative mechanism
/// available at the global scope" WinterTC's Minimum Common API §6 blesses. So
/// <c>WorkerGlobalScope.prototype</c> inherits from <c>%Object.prototype%</c>, and
/// <c>self instanceof EventTarget</c> stays false, because it would otherwise be an <c>instanceof</c> that
/// lied about a relationship the object does not have. §5.3 names what such a global must expose —
/// <c>onerror</c>, <c>onunhandledrejection</c>, <c>onrejectionhandled</c> and <c>self</c> — and all four are
/// here.
/// </para>
/// <para>
/// <b>The members stay own properties of the global object</b>, where the two interface prototypes carry only
/// <c>constructor</c> and an <c>@@toStringTag</c>. That is not an oversight: <c>postMessage</c>, <c>close</c>,
/// <c>importScripts</c>, <c>name</c> and the five event-handler attributes are per <i>connection</i> — they
/// close over the <see cref="WorkerLink"/> this worker was made with — while an interface prototype object is
/// a realm intrinsic built once and shared. Moving them would mean giving the prototype a way to find the
/// connection from its receiver, which buys nothing a script can use: what a script asks of the interface is
/// the brand, and the brand is what the pair provides.
/// </para>
/// <para>
/// <b>Every event-handler attribute here is one entry of the engine's synthetic global listener list</b>
/// (<see cref="GlobalEventTarget"/>), which is the same list <c>addEventListener</c> writes to and the list
/// the worker's hidden port retargets its <c>message</c> events at. That is what makes
/// <c>self.onmessage = f</c> and <c>addEventListener('message', f)</c> equivalent, as they are on a
/// <c>DedicatedWorkerGlobalScope</c> — and it is deliberately unlike <c>MessagePort</c>, whose
/// <c>onmessage</c> setter also starts the port, a rule the specification scopes to that interface alone.
/// </para>
/// <para>
/// Everything is installed <b>non-clobbering</b>, exactly as <c>WebApiRegistration</c> installs a global: a
/// name the host already put on the worker's global object is left as the host left it.
/// </para>
/// </remarks>
internal sealed class WorkerGlobalScope
{
    private readonly WorkerLink _link;
    private readonly Engine _engine;
    private readonly Realm _realm;

    private WorkerGlobalScope(WorkerLink link, Engine engine, Realm realm)
    {
        _link = link;
        _engine = engine;
        _realm = realm;
    }

    /// <summary>
    /// Step 8 of the construction: installs the worker names on <paramref name="link"/>'s worker engine.
    /// Called on the parent's thread, with that engine owned and quiescent.
    /// </summary>
    internal static void Install(WorkerLink link, string name)
    {
        var engine = link.Worker;

        // The PRINCIPAL realm, deliberately, and for WebApiRegistration's reason: these globals belong to the
        // engine's own realm and to no other, and a ShadowRealm carries none of them.
        var realm = engine._mainRealm;
        var global = realm.GlobalObject;

        var scope = new WorkerGlobalScope(link, engine, realm);

        // The brand, before any name is installed. Assigning the global object's [[Prototype]] is safe here and
        // only here: the engine has just been built and has evaluated nothing, so no inline cache holds a
        // resolved binding and no script has observed the old chain. Both interface objects are built by
        // reaching the derived one, whose [[Prototype]] is the base one — WebIDL's rule for an inherited
        // interface — so the two are wired to each other before either is named.
        //
        // Non-clobbering, like every install below it: the chain is inserted only where the object still has
        // the [[Prototype]] every ordinary global starts with. A provider that built its worker engine on a
        // Host.CreateGlobalObject of its own, and gave that object a chain, keeps the chain it chose — and
        // `self instanceof DedicatedWorkerGlobalScope` then answers false, which is the truth about that
        // object rather than a claim we planted on it.
        var dedicated = realm.Intrinsics.DedicatedWorkerGlobalScope;
        if (ReferenceEquals(global._prototype, realm.Intrinsics.Object.PrototypeObject))
        {
            global._prototype = dedicated.PrototypeObject;
        }

        // [Exposed=Worker] and [Exposed=DedicatedWorker]: these two names exist on a worker's global and on no
        // other, which is why they are installed here rather than in WebApiRegistration. Non-clobbering and
        // non-enumerable, exactly as an interface object is installed there —
        // https://webidl.spec.whatwg.org/#es-interfaces.
        InstallDescriptor(global, "WorkerGlobalScope", new PropertyDescriptor(realm.Intrinsics.WorkerGlobalScope, PropertyFlag.NonEnumerable));
        InstallDescriptor(global, "DedicatedWorkerGlobalScope", new PropertyDescriptor(dedicated, PropertyFlag.NonEnumerable));

        // WebIDL operations on the global are writable, enumerable and configurable —
        // https://webidl.spec.whatwg.org/#es-operations.
        InstallValue(global, "postMessage", scope.CreateFunction("postMessage", 1, scope.PostMessage));
        InstallValue(global, "close", scope.CreateFunction("close", 0, scope.Close));

        // https://html.spec.whatwg.org/multipage/workers.html#import-scripts-into-worker-global-scope, whose
        // step 1 is "If worker global scope's type is 'module', throw a TypeError exception." So the function
        // is PRESENT and throws, and `typeof importScripts === 'function'` answers true exactly as it does in
        // a browser — the one place this family's absent-rather-than-throwing convention and the standard
        // disagree, and the standard wins because it is prescribing the throw itself.
        InstallValue(global, "importScripts", scope.CreateFunction("importScripts", 0, scope.ImportScripts));

        // https://html.spec.whatwg.org/multipage/workers.html#dom-dedicatedworkerglobalscope-name. A plain
        // writable data property is the [Replaceable] simplification `self` is already installed with.
        InstallValue(global, "name", JsString.Create(name));

        scope.InstallEventHandler(global, "onmessage", JsMessagePort.MessageEventType);
        scope.InstallEventHandler(global, "onmessageerror", JsMessagePort.MessageErrorEventType);

        // WinterTC §5.3's three. The events already fire at this same target under
        // WebApiFeatures.GlobalEvents; what these add is the attribute form of registering for them.
        //
        // `onerror` is the legacy shape, and it is legacy because the target is a global scope rather than
        // because of anything this feature does: HTML's event handler processing algorithm gives an ErrorEvent
        // named `error` fired at a WindowOrWorkerGlobalScope its own invocation — «message, filename, lineno,
        // colno, error», where returning TRUE cancels — and JsEventTarget implements exactly that, keyed on
        // JsEventTarget.IsGlobalScope. This attribute is therefore what decides notHandled on the worker side,
        // which is the bool the parent-side relay is gated on: an onerror returning true is a worker saying it
        // has dealt with its own failure, and the parent is not told.
        scope.InstallEventHandler(global, "onerror", GlobalEventNames.ErrorName);
        scope.InstallEventHandler(global, "onunhandledrejection", GlobalEventNames.UnhandledRejectionName);
        scope.InstallEventHandler(global, "onrejectionhandled", GlobalEventNames.RejectionHandledName);
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/workers.html#dom-dedicatedworkerglobalscope-postmessage — the
    /// worker half of the connection's port pair.
    /// </summary>
    /// <remarks>
    /// Both WebIDL overloads, resolved the way <c>MessagePort.postMessage</c> resolves them: an iterable
    /// second argument is the <c>sequence&lt;object&gt;</c> arm and anything else is the
    /// <c>StructuredSerializeOptions</c> dictionary. The list travels verbatim, which is what lets a
    /// <c>MessagePort</c> and a transferable stream ride back to the parent untouched.
    /// </remarks>
    private JsValue PostMessage(JsValue thisObject, JsCallArguments arguments)
    {
        if (arguments.Length == 0)
        {
            Throw.TypeError(_realm, "Failed to execute 'postMessage' on 'DedicatedWorkerGlobalScope': 1 argument required, but only 0 present.");
        }

        const string Operation = "postMessage' on 'DedicatedWorkerGlobalScope";
        var argument = arguments.At(1);

        var transferList = argument is ObjectInstance && JsValue.GetMethod(_realm, argument, GlobalSymbolRegistry.Iterator) is not null
            ? StructuredSerializeOptions.ReadTransferSequence(_realm, argument, Operation)
            : StructuredSerializeOptions.ReadTransferOption(_realm, argument, Operation);

        _link.PostFromWorker(_realm, arguments[0], transferList);
        return JsValue.Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/workers.html#dom-dedicatedworkerglobalscope-close — <i>close a
    /// worker</i>: discard the tasks on the worker's own queues and set the closing flag. It pointedly does
    /// <b>not</b> abort the running script, and pointedly does not empty the parent-side queue.
    /// </summary>
    private JsValue Close(JsValue thisObject, JsCallArguments arguments)
    {
        _link.CloseFromWorker();
        return JsValue.Undefined;
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/workers.html#import-scripts-into-worker-global-scope, step 1.
    /// </summary>
    private JsValue ImportScripts(JsValue thisObject, JsCallArguments arguments)
    {
        Throw.TypeError(_realm, "Failed to execute 'importScripts' on 'DedicatedWorkerGlobalScope': Module scripts don't support importScripts(). Use a static or dynamic import instead.");
        return JsValue.Undefined;
    }

    private ClrFunction CreateFunction(string name, int length, JsCallDelegate body)
        => new(_engine, _realm, name, body, length, PropertyFlag.Configurable);

    /// <summary>
    /// An event handler IDL attribute over the engine's synthetic global listener list —
    /// https://html.spec.whatwg.org/multipage/webappapis.html#event-handler-idl-attributes, whose accessor
    /// pair is configurable and enumerable.
    /// </summary>
    private void InstallEventHandler(ObjectInstance global, string attribute, string eventType)
    {
        var getter = CreateFunction(attribute, 0, (_, _) => WorkerErrors.GetEventHandler(Target, eventType));
        var setter = CreateFunction(attribute, 1, (_, arguments) =>
        {
            WorkerErrors.SetEventHandler(Target, eventType, arguments.At(0));
            return JsValue.Undefined;
        });

        InstallDescriptor(global, attribute, new GetSetPropertyDescriptor(getter, setter, PropertyFlag.Configurable | PropertyFlag.Enumerable));
    }

    /// <summary>
    /// The listener list every one of these attributes writes to, and the one the worker's hidden port
    /// dispatches at.
    /// </summary>
    /// <remarks>
    /// Read per call rather than captured, because <c>ResetTransientEvaluationState</c> replaces the target
    /// when an evaluation cycle ends. A restore on the worker also ends the connection, so what these
    /// attributes write to afterwards is a listener list nothing will ever dispatch at; reading it fresh is
    /// what keeps that a dead end rather than a stale reference to the previous cycle's list.
    /// </remarks>
    private GlobalEventTarget Target => _engine._webApi!.GlobalEventTarget;

    private static void InstallValue(ObjectInstance global, string name, JsValue value)
        => InstallDescriptor(global, name, new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable));

    private static void InstallDescriptor(ObjectInstance global, string name, PropertyDescriptor descriptor)
    {
        // Non-clobbering, and probing rather than reading, so that a host's own lazy global is not forced into
        // existence merely by our looking — WebApiRegistration.Install's reasoning, unchanged.
        if (global.HasOwnProperty(JsString.Create(name)))
        {
            return;
        }

        global.SetProperty(name, descriptor);
    }
}
#endif
