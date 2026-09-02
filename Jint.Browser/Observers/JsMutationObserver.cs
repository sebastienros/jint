using AngleSharp.Dom;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Observers;

/// <summary>
/// A <c>MutationObserver</c>: the callback, the registrations AngleSharp keeps for it, and the records it has
/// not delivered yet.
/// </summary>
/// <remarks>
/// <para>
/// <b>The records are AngleSharp's; the timing is Jint's.</b> AngleSharp does the whole of DOM §4.3.4 —
/// walking a mutated node's inclusive ancestors, matching each against the registered observer list, honouring
/// <c>subtree</c> and <c>attributeFilter</c>, and clearing <c>oldValue</c> for an observer that did not ask
/// for it. What it cannot do is <em>when</em>: with no <c>IEventLoop</c> service registered — and registering
/// one would make a step of the parse genuinely asynchronous, which is the parser hop
/// <c>Runtime/PageDocument</c> exists to avoid — AngleSharp's own queue runs inline, so its callback fires
/// synchronously in the middle of <c>appendChild</c>. That inline call is used as the <em>arrival</em> of a
/// record and nothing more: the records are parked here, and <see cref="MutationObserverLane"/> queues one
/// microtask on the engine's job queue to deliver them at the microtask checkpoint, which is where
/// <a href="https://dom.spec.whatwg.org/#notify-mutation-observers">notify mutation observers</a> belongs.
/// </para>
/// <para>
/// <b>Two AngleSharp behaviours are corrected here rather than worked around silently</b>, and both are in
/// <c>Jint.Browser/AGENTS.md</c>'s divergence table. Its <c>observe</c> validation raises a
/// <c>DomException</c> where DOM raises a <c>TypeError</c>, and it resolves the dictionary's optional members
/// differently from the specification — so the flags are resolved and validated here and handed over already
/// settled, which leaves AngleSharp's own checks unreachable. And its <c>Disconnect</c> unregisters the
/// observer from the document but leaves its registration list populated, so a later <c>observe</c> of any
/// node silently resurrects every node it used to watch; a disconnect therefore throws its AngleSharp
/// observer away and the next <c>observe</c> starts a new one.
/// </para>
/// </remarks>
internal sealed class JsMutationObserver : ObjectInstance
{
    private readonly PageRuntime _runtime;
    private readonly ICallable _callback;
    private readonly List<IMutationRecord> _records = [];
    private AngleSharp.Dom.MutationObserver _source;

    internal JsMutationObserver(PageRuntime runtime, ObjectInstance prototype, ICallable callback)
        : base(runtime.Engine)
    {
        _runtime = runtime;
        _callback = callback;
        _source = NewSource();
        Prototype = prototype;
    }

    /// <inheritdoc />
    public override string ToString() => "[object MutationObserver]";

    /// <summary>The receiver check every member of the interface starts with.</summary>
    internal static JsMutationObserver Brand(JsValue thisObject, string member)
    {
        if (thisObject is JsMutationObserver observer)
        {
            return observer;
        }

        var message = "Failed to execute '" + member + "' on 'MutationObserver': Illegal invocation";

        if (thisObject is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return null!;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-mutationobserver-observe — register <paramref name="arguments"/>'s
    /// node with the options it asks for, replacing any registration this observer already has for it.
    /// </summary>
    internal JsValue Observe(JsValue[] arguments)
    {
        var realm = _runtime.Engine._mainRealm;

        if (arguments.At(0) is not IDomWrapper { DomTarget: INode node })
        {
            Throw.TypeError(realm, "Failed to execute 'observe' on 'MutationObserver': parameter 1 is not of type 'Node'.");
            return JsValue.Undefined;
        }

        var options = arguments.At(1) as ObjectInstance;
        var childList = Flag(options, "childList") ?? false;
        var subtree = Flag(options, "subtree") ?? false;
        var attributeOldValue = Flag(options, "attributeOldValue");
        var characterDataOldValue = Flag(options, "characterDataOldValue");
        var filter = Filter(options);

        // Steps 2-4: an option that only makes sense with attributes (or with characterData) turns that one
        // on when the dictionary left it out. "Left out" and "present and false" are different: { attributes:
        // false, attributeOldValue: false } is a TypeError, { attributeOldValue: false } is not.
        var attributes = Flag(options, "attributes") ?? (attributeOldValue is not null || filter is not null);
        var characterData = Flag(options, "characterData") ?? characterDataOldValue is not null;

        if (!childList && !attributes && !characterData)
        {
            Throw.TypeError(realm, "Failed to execute 'observe' on 'MutationObserver': The options object must set at least one of 'attributes', 'characterData', or 'childList' to true.");
        }

        if (attributeOldValue == true && !attributes)
        {
            Throw.TypeError(realm, "Failed to execute 'observe' on 'MutationObserver': The options object may only set 'attributeOldValue' to true when 'attributes' is true or not present.");
        }

        if (filter is not null && !attributes)
        {
            Throw.TypeError(realm, "Failed to execute 'observe' on 'MutationObserver': The options object may only set 'attributeFilter' when 'attributes' is true or not present.");
        }

        if (characterDataOldValue == true && !characterData)
        {
            Throw.TypeError(realm, "Failed to execute 'observe' on 'MutationObserver': The options object may only set 'characterDataOldValue' to true when 'characterData' is true or not present.");
        }

        // Every flag is passed as a settled value, so none of AngleSharp's own defaulting or validation runs.
        _source.Connect(
            node,
            childList,
            subtree,
            attributes,
            characterData,
            attributeOldValue == true,
            characterDataOldValue == true,
            filter);

        return JsValue.Undefined;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-mutationobserver-disconnect — empty the registration list and the
    /// record queue.
    /// </summary>
    internal JsValue Disconnect()
    {
        _source.Disconnect();

        // A fresh one, because AngleSharp's Disconnect leaves its own registration list in place: reusing it
        // would make the next observe() re-register every node this one used to watch.
        _source = NewSource();
        _records.Clear();
        _runtime.MutationObservers.Withdraw(this);
        return JsValue.Undefined;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-mutationobserver-takerecords — the queued records, and the queue is
    /// emptied.
    /// </summary>
    internal JsValue TakeRecords()
    {
        var records = Drain();
        _runtime.MutationObservers.Withdraw(this);
        return records;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#notify-mutation-observers, for this observer: hand the queued records to
    /// the callback, with the observer itself as both the second argument and the receiver.
    /// </summary>
    internal void Deliver()
    {
        if (_records.Count == 0)
        {
            return;
        }

        var records = Drain();

        try
        {
            _callback.Call(this, [records, this]);
        }
        catch (JavaScriptException exception)
        {
            // WebIDL's "report the exception": the page survives a callback that threw, exactly as it
            // survives a timer callback that threw.
            _runtime.Recorder.Add(new PageError(
                PageErrorKind.UncaughtCallbackError,
                PageRecorder.Diagnostics.Describe(exception.Error, exception),
                "MutationObserver"));
        }
    }

    private AngleSharp.Dom.MutationObserver NewSource() => new(OnRecords);

    /// <summary>
    /// What AngleSharp calls, synchronously, from inside the mutation. Nothing but bookkeeping happens here —
    /// no script runs — so re-entering the DOM operation that is still running is safe.
    /// </summary>
    private void OnRecords(IEnumerable<IMutationRecord> records, AngleSharp.Dom.MutationObserver source)
    {
        foreach (var record in records)
        {
            _records.Add(new DeliveredMutationRecord(record));
        }

        if (_records.Count > 0)
        {
            _runtime.MutationObservers.Enlist(this);
        }
    }

    private JsArray Drain()
    {
        var realm = _runtime.Engine._mainRealm;
        var values = new JsValue[_records.Count];

        for (var i = 0; i < _records.Count; i++)
        {
            values[i] = _runtime.Dom.Wrap(_records[i]);
        }

        _records.Clear();
        return realm.Intrinsics.Array.ConstructFast(values);
    }

    /// <summary>
    /// A dictionary member declared <c>boolean</c> with no default: <see langword="null"/> means the member
    /// was not there, which is what steps 2-4 of <c>observe</c> distinguish from <see langword="false"/>.
    /// </summary>
    private static bool? Flag(ObjectInstance? options, string name)
    {
        if (options is null)
        {
            return null;
        }

        var value = options.Get(name);
        return value.IsUndefined() ? null : TypeConverter.ToBoolean(value);
    }

    /// <summary>
    /// <c>attributeFilter</c>, a <c>sequence&lt;DOMString&gt;</c>. <see langword="null"/> means absent, which
    /// is what turns <c>attributes</c> on by itself.
    /// </summary>
    private static string[]? Filter(ObjectInstance? options)
    {
        if (options?.Get("attributeFilter") is not { } value || value.IsUndefined() || value.IsNull())
        {
            return null;
        }

        if (value is not ObjectInstance list)
        {
            Throw.TypeErrorNoEngine("Failed to execute 'observe' on 'MutationObserver': The provided value cannot be converted to a sequence.");
            return null;
        }

        var length = (int) TypeConverter.ToUint32(list.Get("length"));
        var names = new string[length];

        for (var i = 0; i < length; i++)
        {
            names[i] = TypeConverter.ToString(list.Get(i.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        return names;
    }
}
