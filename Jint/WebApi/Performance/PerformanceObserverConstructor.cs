#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Performance;

/// <summary>
/// The <c>PerformanceObserver</c> interface object.
/// <para>
/// https://w3c.github.io/performance-timeline/#dom-performanceobserver
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// It carries the one static member of the interface, <c>supportedEntryTypes</c>, which is what a script's
/// feature detection reads before it observes anything — the specification's own example opens with it.
/// </para>
/// <para>
/// The two entry types Jint produces are <c>mark</c> and <c>measure</c>; every other type
/// https://w3c.github.io/timing-entrytypes-registry/ names belongs to a document's navigation, its
/// subresources or its rendering, none of which an embedded interpreter has. Observing an unsupported type
/// is not an error — <c>observe()</c> filters unknown names out and, if nothing is left, registers nothing —
/// so a script written for a browser degrades rather than throws.
/// </para>
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class PerformanceObserverConstructor : Constructor
{
    private static readonly JsString _functionName = new("PerformanceObserver");

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dfn-frozen-array-of-supported-entry-types — "in
    /// alphabetical order", which <c>performance-timeline/supportedEntryTypes.any.js</c> checks pairwise.
    /// </summary>
    private static readonly string[] _supportedEntryTypes = ["mark", "measure"];

    /// <summary>
    /// The one array every read of <c>supportedEntryTypes</c> answers with. <c>[SameObject]</c> in the IDL,
    /// so the identity is part of the contract and not merely a saving.
    /// </summary>
    private JsArray? _supported;

    internal PerformanceObserverConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new PerformanceObserverPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal PerformanceObserverPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performanceobserver-performanceobserver — "create a new
    /// PerformanceObserver object with its observer callback set to callback".
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        // The argument is a callback function, which WebIDL converts by requiring it to be callable —
        // https://webidl.spec.whatwg.org/#es-callback-function.
        var callback = arguments.At(0);
        if (callback is not ICallable)
        {
            Throw.TypeError(_realm, "Failed to construct 'PerformanceObserver': parameter 1 is not of type 'PerformanceObserverCallback'.");
        }

        var performance = _realm.Intrinsics.PerformanceObject;

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.PerformanceObserver.PrototypeObject,
            static (Engine engine, Realm realm, (JsValue Callback, JsPerformance Performance) state)
                => new JsPerformanceObserver(engine, realm, state.Callback, state.Performance),
            (Callback: callback, Performance: performance));
    }

    /// <summary>
    /// https://w3c.github.io/performance-timeline/#dom-performanceobserver-supportedentrytypes — a
    /// <c>FrozenArray</c>, so an ordinary array whose integrity level is frozen, built once and answered by
    /// identity from then on.
    /// </summary>
    [JsAccessor("supportedEntryTypes", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsArray SupportedEntryTypesGet(JsValue thisObject)
    {
        if (_supported is not null)
        {
            return _supported;
        }

        var values = new List<JsValue>(_supportedEntryTypes.Length);
        foreach (var type in _supportedEntryTypes)
        {
            values.Add(JsString.Create(type));
        }

        var array = _realm.Intrinsics.Array.ConstructFast(values);
        array.SetIntegrityLevel(IntegrityLevel.Frozen);
        _supported = array;
        return array;
    }

    /// <summary>
    /// Whether <paramref name="entryType"/> is one this engine produces — the membership test both arms of
    /// <c>observe()</c> filter with.
    /// </summary>
    internal static bool IsSupportedEntryType(string entryType)
    {
        foreach (var supported in _supportedEntryTypes)
        {
            if (string.Equals(supported, entryType, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
#endif
