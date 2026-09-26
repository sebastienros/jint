using Jint.HtmlParser;
using Jint.Browser.Dom;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.Observers;

/// <summary>
/// A <c>MutationObserver</c>: native registrations and records delivered on the engine's microtask queue.
/// </summary>
/// <remarks>
/// Native mutation matching owns subtree, attribute filters, old values and detached-subtree transients.
/// The native subscription callback performs scheduling only; no script runs inside a DOM mutation.
/// <see cref="MutationObserverLane"/> delivers records at the
/// <a href="https://dom.spec.whatwg.org/#notify-mutation-observers">mutation observer checkpoint</a>.
/// Browser resolves WebIDL options and raises script TypeError before registering native options.
/// </remarks>
internal sealed class JsMutationObserver : ObjectInstance, IDisposable
{
    private readonly PageRuntime _runtime;
    private readonly ICallable _callback;
    private readonly MutationSubscription _source;

    internal JsMutationObserver(PageRuntime runtime, ObjectInstance prototype, ICallable callback)
        : base(runtime.Engine)
    {
        _runtime = runtime;
        _callback = callback;
        _source = new MutationSubscription();
        _source.PendingRecord = _ => _runtime.MutationObservers.Enlist(this);
        Prototype = prototype;
    }

    /// <inheritdoc />
    public override string ToString() => "[object MutationObserver]";

    void IDisposable.Dispose()
    {
        _source.Dispose();
        _runtime.MutationObservers.Withdraw(this);
    }

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

        if (arguments.At(0) is not IDomWrapper { DomTarget: Node node })
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

        _source.Observe(node, new MutationObserverOptions
        {
            ChildList = childList,
            Subtree = subtree,
            Attributes = attributes,
            CharacterData = characterData,
            AttributeOldValue = attributeOldValue == true,
            CharacterDataOldValue = characterDataOldValue == true,
            AttributeFilter = filter,
        });

        return JsValue.Undefined;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-mutationobserver-disconnect — empty the registration list and the
    /// record queue.
    /// </summary>
    internal JsValue Disconnect()
    {
        _source.Disconnect();

        _runtime.MutationObservers.Withdraw(this);
        return JsValue.Undefined;
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#dom-mutationobserver-takerecords — the queued records, and the queue is
    /// emptied.
    /// </summary>
    internal JsValue TakeRecords()
    {
        // The queued delivery must still clear detached-subtree transient registrations.
        return WrapRecords(_source.TakeRecords());
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#notify-mutation-observers, for this observer: hand the queued records to
    /// the callback, with the observer itself as both the second argument and the receiver.
    /// </summary>
    internal void Deliver()
    {
        var nativeRecords = _source.TakeRecordsForDelivery();
        if (nativeRecords.Count == 0)
        {
            return;
        }

        var records = WrapRecords(nativeRecords);

        try
        {
            _callback.Call(this, [records, this]);
        }
        catch (JavaScriptException exception)
        {
            // WebIDL's "report the exception": the page survives a callback that threw, exactly as it
            // survives a timer callback that threw.
            _runtime.Recorder.Add(
                PageErrorKind.UncaughtCallbackError,
                PageRecorder.Diagnostics.Describe(exception.Error, exception),
                "MutationObserver");
        }
    }

    private JsArray WrapRecords(IReadOnlyList<MutationRecord> records)
    {
        var realm = _runtime.Engine._mainRealm;
        var values = new JsValue[records.Count];
        for (var i = 0; i < records.Count; i++)
        {
            values[i] = _runtime.Dom.Wrap(records[i]);
        }
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
