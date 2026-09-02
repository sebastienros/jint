#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.DomException;

namespace Jint.WebApi.Performance;

/// <summary>
/// <c>PerformanceObserver.prototype</c> — the interface prototype object.
/// <para>
/// https://w3c.github.io/performance-timeline/#dom-performanceobserver
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The <i>list of registered performance observer objects</i> these three operate on is the engine's, not
/// this object's — see <see cref="PerformanceObserverRegistry"/> for where it lives and why.
/// </para>
/// <para>
/// The two <c>observe()</c> shapes stack differently and that is deliberate rather than an accident of the
/// implementation: an <c>entryTypes</c> call replaces the observer's whole options list, a <c>type</c> call
/// appends to it (replacing only an entry naming the same type), and mixing the two is an
/// <c>InvalidModificationError</c>. <see cref="PerformanceObserverType"/> is the flag that decides.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class PerformanceObserverPrototype : Prototype
{
    private static readonly JsString _buffered = new("buffered");
    private static readonly JsString _entryTypes = new("entryTypes");
    private static readonly JsString _typeKey = new("type");

    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly PerformanceObserverConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString PerformanceObserverToStringTag = new("PerformanceObserver");

    internal PerformanceObserverPrototype(
        Engine engine,
        Realm realm,
        PerformanceObserverConstructor constructor,
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
    /// https://w3c.github.io/performance-timeline/#dom-performanceobserver-observe
    /// </summary>
    [JsFunction(Name = "observe", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Observe(JsValue thisObject, JsValue optionsArgument)
    {
        const string Context = "Failed to execute 'observe' on 'PerformanceObserver'";

        var observer = Brand(thisObject, "observe");
        var options = ReadInit(optionsArgument, Context);

        // Step 2: "If options's entryTypes and type members are both omitted, then throw a TypeError."
        if (options.EntryTypes is null && options.Type is null)
        {
            Throw.TypeError(_realm, Context + ": either 'entryTypes' or 'type' must be present.");
        }

        // Step 3: "If options's entryTypes is present and any other member is also present, then throw a
        // TypeError." `buffered` counts, which is why the reader keeps its presence rather than its value.
        if (options.EntryTypes is not null && (options.Type is not null || options.HasBuffered))
        {
            Throw.TypeError(_realm, Context + ": 'entryTypes' cannot be combined with any other member.");
        }

        // Step 4, first half: the observer commits to one of the two shapes, and refuses the other from then
        // on. The refusal is an InvalidModificationError DOMException, not a TypeError.
        if (observer.ObserverType == PerformanceObserverType.Undefined)
        {
            observer.ObserverType = options.EntryTypes is not null
                ? PerformanceObserverType.Multiple
                : PerformanceObserverType.Single;
        }
        else if (observer.ObserverType == PerformanceObserverType.Single && options.EntryTypes is not null)
        {
            ThrowInvalidModification(Context + ": this observer has already been used with 'type'.");
        }
        else if (observer.ObserverType == PerformanceObserverType.Multiple && options.Type is not null)
        {
            ThrowInvalidModification(Context + ": this observer has already been used with 'entryTypes'.");
        }

        observer.RequiresDroppedEntries = true;

        var registry = UserTiming.RequireState(_engine, "PerformanceObserver.observe").PerformanceObservers;

        if (observer.ObserverType == PerformanceObserverType.Multiple)
        {
            // "Remove all types from entry types that are not contained in the frozen array of supported
            // entry types", then "if the resulting sequence is empty, abort these steps" — which is what
            // makes observe({entryTypes: []}) and observe({entryTypes: ['nope']}) silent no-ops rather than
            // errors, and is exactly what a browser does with a type it does not implement.
            var supported = Supported(options.EntryTypes!);
            if (supported.Length == 0)
            {
                return Undefined;
            }

            var registration = registry.Find(observer) ?? registry.Add(observer);
            registration.Options.Clear();
            registration.Options.Add(new PerformanceObserverOptions(Type: null, supported, options.Buffered));
            return Undefined;
        }

        var type = options.Type!;
        if (!PerformanceObserverConstructor.IsSupportedEntryType(type))
        {
            return Undefined;
        }

        var single = registry.Find(observer) ?? registry.Add(observer);
        var replacement = new PerformanceObserverOptions(type, EntryTypes: null, options.Buffered);

        var replaced = false;
        for (var i = 0; i < single.Options.Count; i++)
        {
            if (string.Equals(single.Options[i].Type, type, StringComparison.Ordinal))
            {
                single.Options[i] = replacement;
                replaced = true;
                break;
            }
        }

        if (!replaced)
        {
            single.Options.Add(replacement);
        }

        if (!options.Buffered)
        {
            return Undefined;
        }

        // The buffered flag: replay what the timeline already holds of this type, then queue the delivery
        // task — which is what lets an observer registered after the fact still see the entries it missed.
        observer.Performance.ReplayInto(observer, type);
        registry.QueueTask();
        return Undefined;
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performanceobserver-disconnect — deregister, and drop
    /// both the entries queued for the callback and the types it asked for.
    /// </summary>
    /// <remarks>
    /// Dropping the options list is what makes a <c>disconnect()</c> followed by a fresh <c>observe()</c>
    /// observe only the new type; the observer type itself is deliberately <i>not</i> reset, because the
    /// specification's rule is about the object and not about one registration.
    /// </remarks>
    [JsFunction(Name = "disconnect", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue Disconnect(JsValue thisObject)
    {
        var observer = Brand(thisObject, "disconnect");
        UserTiming.RequireState(_engine, "PerformanceObserver.disconnect").PerformanceObservers.Remove(observer);
        observer.EmptyObserverBuffer();
        return Undefined;
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performanceobserver-takerecords — "return a copy of
    /// this's observer buffer, and also empty this's observer buffer".
    /// </summary>
    [JsFunction(Name = "takeRecords", Length = 0, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsArray TakeRecords(JsValue thisObject)
    {
        var observer = Brand(thisObject, "takeRecords");
        var entries = observer.TakeObserverBuffer();

        // The buffer is handed out in the order the entries were queued, which is already chronological
        // unless a mark carried an explicit startTime — the same sort every other reader of a list of
        // entries runs.
        return PerformanceEntryFilter.Filter(observer.Realm, entries, name: null, type: null);
    }

    /// <summary>
    /// The <c>PerformanceObserverInit</c> dictionary conversion,
    /// https://w3c.github.io/performance-timeline/#dictdef-performanceobserverinit.
    /// </summary>
    /// <remarks>
    /// The members are read in <i>lexicographical order of their identifiers</i>
    /// (https://webidl.spec.whatwg.org/#es-dictionary) — <c>buffered</c>, <c>entryTypes</c>, <c>type</c> —
    /// which is observable when one of them is an accessor. Presence is what the two <c>TypeError</c>s above
    /// are about, so an explicit <see langword="undefined"/> is <i>absent</i> and
    /// <c>observe({ type: 'mark', entryTypes: undefined })</c> is a legal single-type observe.
    /// </remarks>
    private ObserverInit ReadInit(JsValue value, string context)
    {
        if (value is not ObjectInstance dictionary)
        {
            // Step 1 of the dictionary conversion: the two nullish values are the empty dictionary, and
            // anything else that is not an object is refused at the boundary.
            if (!value.IsUndefined() && !value.IsNull())
            {
                Throw.TypeError(_realm, context + ": the provided value is not of type 'PerformanceObserverInit'.");
            }

            return default;
        }

        var bufferedValue = dictionary.Get(_buffered);
        var hasBuffered = !bufferedValue.IsUndefined();
        var buffered = hasBuffered && TypeConverter.ToBoolean(bufferedValue);

        var entryTypesValue = dictionary.Get(_entryTypes);
        var entryTypes = entryTypesValue.IsUndefined() ? null : ReadStringSequence(entryTypesValue, context);

        var typeValue = dictionary.Get(_typeKey);
        var type = typeValue.IsUndefined() ? null : TypeConverter.ToString(typeValue);

        return new ObserverInit(entryTypes, type, hasBuffered, buffered);
    }

    /// <summary>
    /// The <c>sequence&lt;DOMString&gt;</c> conversion, https://webidl.spec.whatwg.org/#js-sequence.
    /// </summary>
    /// <remarks>
    /// Step 1 is "If Type(V) is not Object, throw a TypeError", so a bare string is refused even though a
    /// JavaScript string is itself iterable — which is the point of
    /// <c>performance-timeline/po-observe.any.js</c>'s first row, <c>observe({entryTypes: "mark"})</c>.
    /// </remarks>
    private string[] ReadStringSequence(JsValue value, string context)
    {
        if (value is not ObjectInstance)
        {
            Throw.TypeError(_realm, context + ": 'entryTypes' is not a sequence.");
        }

        var names = new List<string>();
        var iterator = value.GetIterator(_realm);

        try
        {
            while (iterator.TryIteratorStepValue(out var element))
            {
                names.Add(TypeConverter.ToString(element));
            }
        }
        finally
        {
            iterator.Close(CompletionType.Normal);
        }

        return names.ToArray();
    }

    /// <summary>The supported subset of an <c>entryTypes</c> sequence, in the order it was given.</summary>
    private static string[] Supported(string[] entryTypes)
    {
        var kept = new List<string>(entryTypes.Length);
        foreach (var entryType in entryTypes)
        {
            if (PerformanceObserverConstructor.IsSupportedEntryType(entryType) && !kept.Contains(entryType))
            {
                kept.Add(entryType);
            }
        }

        return kept.ToArray();
    }

    private void ThrowInvalidModification(string message)
    {
        var exception = _realm.Intrinsics.DomException.CreateException(DomExceptionNames.InvalidModification, message);
        var location = _engine._lastSyntaxElement?.Location ?? default;
        Throw.JavaScriptException(_engine, exception, in location);
    }

    /// <summary>
    /// The WebIDL brand check every member performs: a receiver that is not a platform object implementing
    /// the interface raises a <c>TypeError</c>.
    /// </summary>
    private JsPerformanceObserver Brand(JsValue thisObject, string operation)
    {
        if (thisObject is JsPerformanceObserver observer)
        {
            return observer;
        }

        Throw.TypeError(
            _realm,
            $"Failed to execute '{operation}' on 'PerformanceObserver': illegal invocation, receiver is not a PerformanceObserver.");
        return null!;
    }

    /// <summary>
    /// The dictionary as read, with each member's presence kept beside its value because
    /// <c>observe()</c>'s first three steps are about presence and not about the values.
    /// </summary>
    private readonly record struct ObserverInit(string[]? EntryTypes, string? Type, bool HasBuffered, bool Buffered);
}
#endif
