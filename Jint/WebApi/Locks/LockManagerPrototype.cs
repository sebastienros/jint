#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Abort;
using Jint.WebApi.DomException;

namespace Jint.WebApi.Locks;

/// <summary>
/// <c>LockManager.prototype</c> — the interface prototype object, and where both members of
/// <c>navigator.locks</c> live.
/// <para>
/// https://w3c.github.io/web-locks/#lockmanager-class
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>request()</c> asks for a resource name, runs the callback once the lock is granted, and answers with a
/// promise that settles when the lock is released — or rejects when the request is aborted.
/// <c>query()</c> answers with a snapshot of what is held and what is waiting.
/// </para>
/// <para>
/// <b>Neither method ever throws.</b> Both return promises, and WebIDL turns every exception a
/// promise-returning operation would raise — a bad argument, a receiver that is not a lock manager — into a
/// rejection of the returned promise instead ("And then, if an exception E was thrown: if op has a return
/// type that is a promise type, then return ! Call(%Promise.reject%, %Promise%, «E»)",
/// https://webidl.spec.whatwg.org/#dfn-create-operation-function).
/// </para>
/// <para>
/// <b>A grant is a task, not a microtask</b>, which is the ordering the specification prescribes twice: both
/// "enqueue the following steps on callback's relevant settings object's responsible event loop" — the grant
/// steps in <i>process the lock request queue</i>, and the <c>ifAvailable</c> miss in <i>request a lock</i> —
/// mean a task. So a lock is never granted inside the call that asked for it, however free the resource is,
/// and a <c>controller.abort()</c> on the next line still aborts the request. What happens in the calling
/// turn is only the queueing, under the manager's own gate: that is why two <c>request()</c> calls made in
/// one turn are granted in the order they were made, and why <c>query()</c> — which is a task of its own —
/// already sees both of them.
/// </para>
/// <para>
/// <b>The two operations are here, not on the instance</b>, which is where WebIDL puts them. That is what
/// lets them carry WebIDL's attributes (https://webidl.spec.whatwg.org/#es-operations) without
/// <c>Object.keys(navigator.locks)</c> reporting <c>["request", "query"]</c>, which no implementation does —
/// the enumerability is invisible from an instance with no own properties. Both still brand-check their
/// receiver, and because the return type is a promise that check surfaces as a rejection rather than a
/// throw.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class LockManagerPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly LockManagerConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString LockManagerToStringTag = new("LockManager");

    private static readonly JsString _ifAvailableProperty = new("ifAvailable");
    private static readonly JsString _modeProperty = new("mode");
    private static readonly JsString _signalProperty = new("signal");
    private static readonly JsString _stealProperty = new("steal");

    /// <summary>
    /// https://w3c.github.io/web-locks/#dictdef-lockinfo — a WebIDL dictionary, so an ordinary object with
    /// three data properties in declaration order. A layout rather than three
    /// <c>CreateDataProperty</c> calls so that every row of every snapshot in an engine shares one hidden
    /// class, which is what keeps a <c>state.held.filter(l =&gt; l.name === res)</c> monomorphic.
    /// </summary>
    private static readonly JsObjectLayout _lockInfoLayout = JsObjectLayout.CreateBuilder()
        .Add("name")
        .Add("mode")
        .Add("clientId")
        .Build();

    /// <summary>
    /// https://w3c.github.io/web-locks/#dictdef-lockmanagersnapshot — the same, for the two sequences
    /// <c>query()</c> resolves with.
    /// </summary>
    private static readonly JsObjectLayout _snapshotLayout = JsObjectLayout.CreateBuilder()
        .Add("held")
        .Add("pending")
        .Build();

    internal LockManagerPrototype(
        Engine engine,
        Realm realm,
        LockManagerConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://w3c.github.io/web-locks/#dom-lockmanager-request — the <c>request(name, callback)</c> and
    /// <c>request(name, options, callback)</c> method steps, whose tail is
    /// https://w3c.github.io/web-locks/#request-a-lock.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The callback is always the last argument, which is what the two overloads are for; the distinguishing
    /// argument count is all that separates them, because WebIDL resolves an overload by arity first.
    /// </para>
    /// <para>
    /// Four of the method's steps have no analogue here and are not omissions. The <i>fully active
    /// document</i> check (step 3) needs a document; <i>obtain a lock manager</i> (step 4) cannot fail,
    /// because an engine's manager is either the host's or its own and neither is an opaque origin; and the
    /// name is a <c>DOMString</c> rather than a <c>USVString</c>, so an unpaired surrogate survives it — which
    /// <c>web-locks/resource-names.https.any.js</c> asserts by holding two locks whose names differ only by
    /// one. Step 5's "starts with U+002D HYPHEN-MINUS" is the <i>only</i> name the specification reserves:
    /// the empty string is a legal resource name and that same file requires it to be granted.
    /// </para>
    /// </remarks>
    [JsFunction(Name = "request", Length = 2, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Request(JsValue thisObject, JsValue name, JsValue second, JsValue third, [ArgCount] int argumentCount)
    {
        // Step 10 hoisted: the promise exists before anything can fail, because everything that can fail
        // from here on rejects it rather than throwing.
        var capability = NewPromiseCapability();

        try
        {
            const string What = "Failed to execute 'request' on 'LockManager'";

            var manager = Brand(thisObject, What);

            if (argumentCount < 2)
            {
                Throw.TypeError(_realm, $"{What}: 2 arguments required, but only {argumentCount} present.");
            }

            // WebIDL converts the arguments left to right, so a bad `mode` is reported before a callback that
            // is not callable — which is why the name and the options are read first.
            var resourceName = TypeConverter.ToString(name);
            var callbackValue = argumentCount >= 3 ? third : second;
            var options = argumentCount >= 3 ? second : JsValue.Undefined;

            var (mode, ifAvailable, steal, signal) = ReadOptions(options, What);

            if (callbackValue is not ICallable callback)
            {
                Throw.TypeError(_realm, $"{What}: parameter {(argumentCount >= 3 ? 3 : 2)} is not of type 'Function'.");
                return capability.PromiseInstance;
            }

            // Step 5.
            if (resourceName.Length > 0 && resourceName[0] == '-')
            {
                ThrowDomException(
                    DomExceptionNames.NotSupported,
                    $"{What}: names cannot start with '-'.");
            }

            // Step 6.
            if (steal && ifAvailable)
            {
                ThrowDomException(
                    DomExceptionNames.NotSupported,
                    $"{What}: the 'steal' and 'ifAvailable' options cannot be used together.");
            }

            // Step 7.
            if (steal && mode != LockMode.Exclusive)
            {
                ThrowDomException(
                    DomExceptionNames.NotSupported,
                    $"{What}: the 'steal' option may only be used with exclusive locks.");
            }

            // Step 8.
            if (signal is not null && (steal || ifAvailable))
            {
                ThrowDomException(
                    DomExceptionNames.NotSupported,
                    $"{What}: the 'signal' option cannot be used with 'steal' or 'ifAvailable'.");
            }

            // Step 9: an already-aborted signal rejects with its own reason rather than with an AbortError
            // this engine minted, which is what carries a `controller.abort(reason)` through.
            if (signal is { Aborted: true })
            {
                capability.Reject(signal.Reason);
                return capability.PromiseInstance;
            }

            // Step 11, and then the whole of "request a lock".
            var operation = new LockOperation(_engine, _realm, manager.Manager, callback, capability);
            var request = new LockRequestEntry(manager.Agent, resourceName, mode, operation);

            // Step 2 of "request a lock", before the queueing: an abort that arrives between the two would
            // otherwise find nothing registered and leave the request in the queue forever.
            if (signal is not null)
            {
                operation.AttachSignal(signal, request);
            }

            manager.Manager.Request(request, ifAvailable, steal);
        }
        catch (JavaScriptException ex)
        {
            capability.Reject(ex.Error);
        }

        return capability.PromiseInstance;
    }

    /// <summary>
    /// https://w3c.github.io/web-locks/#dom-lockmanager-query — "enqueue the steps to snapshot the lock
    /// state for manager with promise to the lock task queue".
    /// </summary>
    /// <remarks>
    /// The snapshot is taken in a task rather than in the call, which is what the enqueue means and what
    /// makes the answer include the work queued earlier in the same turn.
    /// </remarks>
    [JsFunction(Name = "query", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Query(JsValue thisObject)
    {
        var capability = NewPromiseCapability();

        try
        {
            var instance = Brand(thisObject, "Failed to execute 'query' on 'LockManager'");
            var manager = instance.Manager;
            var realm = _realm;
            var engine = _engine;

            instance.Agent.Schedule(
                instance.Agent.Generation,
                () => capability.Resolve(BuildSnapshot(engine, realm, manager.Snapshot())));
        }
        catch (JavaScriptException ex)
        {
            capability.Reject(ex.Error);
        }

        return capability.PromiseInstance;
    }

    /// <summary>
    /// https://w3c.github.io/web-locks/#snapshot-the-lock-state, last step: the dictionary the promise is
    /// resolved with.
    /// </summary>
    private static JsObject BuildSnapshot(Engine engine, Realm realm, in LockStateSnapshot snapshot)
        => JsObject.Create(
            engine,
            _snapshotLayout,
            [BuildInfoList(engine, realm, snapshot.Held), BuildInfoList(engine, realm, snapshot.Pending)]);

    private static JsArray BuildInfoList(Engine engine, Realm realm, IReadOnlyList<LockInfo> entries)
    {
        var values = new JsValue[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            values[i] = JsObject.Create(
                engine,
                _lockInfoLayout,
                [JsString.Create(entry.Name), LockModeNames.ToJsString(entry.Mode), JsString.Create(entry.ClientId)]);
        }

        return realm.Intrinsics.Array.CreateArrayFromList(values);
    }

    /// <summary>
    /// The <c>LockOptions</c> dictionary, https://w3c.github.io/web-locks/#dictdef-lockoptions.
    /// </summary>
    /// <remarks>
    /// The members are read in lexicographical order — <c>ifAvailable</c>, <c>mode</c>, <c>signal</c>,
    /// <c>steal</c> — which is the order https://webidl.spec.whatwg.org/#es-dictionary specifies and not the
    /// order the IDL declares them in. It is observable: an options object whose getters throw reports the
    /// first failure in that order.
    /// </remarks>
    private (LockMode Mode, bool IfAvailable, bool Steal, JsAbortSignal? Signal) ReadOptions(JsValue options, string what)
    {
        if (options.IsUndefined() || options.IsNull())
        {
            return (LockMode.Exclusive, false, false, null);
        }

        if (options is not ObjectInstance dictionary)
        {
            Throw.TypeError(_realm, $"{what}: the provided value is not of type 'LockOptions'.");
            return default;
        }

        var ifAvailableValue = dictionary.Get(_ifAvailableProperty);
        var ifAvailable = !ifAvailableValue.IsUndefined() && TypeConverter.ToBoolean(ifAvailableValue);

        var modeValue = dictionary.Get(_modeProperty);
        var mode = modeValue.IsUndefined() ? LockMode.Exclusive : LockModeNames.Parse(_realm, modeValue, what);

        var signalValue = dictionary.Get(_signalProperty);
        JsAbortSignal? signal = null;
        if (!signalValue.IsUndefined())
        {
            // The member is typed AbortSignal, not AbortSignal?, so null is a TypeError rather than "no
            // signal" — https://webidl.spec.whatwg.org/#es-interface.
            if (signalValue is not JsAbortSignal abortSignal)
            {
                Throw.TypeError(_realm, $"{what}: member signal is not of type 'AbortSignal'.");
                return default;
            }

            signal = abortSignal;
        }

        var stealValue = dictionary.Get(_stealProperty);
        var steal = !stealValue.IsUndefined() && TypeConverter.ToBoolean(stealValue);

        return (mode, ifAvailable, steal, signal);
    }

    private void ThrowDomException(string name, string message)
    {
        var exception = _realm.Intrinsics.DomException.CreateException(name, message);
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }

    private PromiseCapability NewPromiseCapability()
        => PromiseConstructor.NewPromiseCapability(_engine, _realm.Intrinsics.Promise);

    /// <summary>
    /// The WebIDL brand check both members perform: a receiver that is not a platform object implementing
    /// the interface raises a <c>TypeError</c> — which, for these two, the caller turns into a rejection.
    /// </summary>
    private JsLockManager Brand(JsValue thisObject, string what)
    {
        if (thisObject is not JsLockManager instance)
        {
            Throw.TypeError(_realm, what + ": illegal invocation, receiver is not a LockManager object.");
            return null!;
        }

        return instance;
    }
}
#endif
