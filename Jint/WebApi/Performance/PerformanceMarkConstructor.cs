#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Performance;

/// <summary>
/// The <c>PerformanceMark</c> interface object.
/// <para>
/// https://w3c.github.io/user-timing/#dom-performancemark
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// <c>PerformanceMark</c> inherits from <c>PerformanceEntry</c>, so its <c>[[Prototype]]</c> is the
/// <c>PerformanceEntry</c> interface object rather than <c>%Function.prototype%</c> —
/// https://webidl.spec.whatwg.org/#interface-object.
/// </para>
/// <para>
/// It is the one entry type with a constructor operation, and constructing one is deliberately <b>not</b> the
/// same as calling <c>performance.mark()</c>: https://w3c.github.io/user-timing/#dom-performance-mark runs
/// this constructor and <i>then</i> queues the entry and adds it to the buffer, so a mark built with
/// <c>new</c> exists as an object but is invisible to <c>getEntries()</c> and unreachable as the start or end
/// of a <c>measure</c>.
/// </para>
/// </remarks>
internal sealed class PerformanceMarkConstructor : Constructor
{
    private static readonly JsString _functionName = new("PerformanceMark");

    internal PerformanceMarkConstructor(Engine engine, Realm realm, PerformanceEntryConstructor entryConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = entryConstructor;
        PrototypeObject = new PerformanceMarkPrototype(engine, realm, this, entryConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal PerformanceMarkPrototype PrototypeObject { get; }

    /// <summary>
    /// https://w3c.github.io/user-timing/#dom-performancemark-performancemark
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        var (name, startTime, detail) = ReadArguments(
            _engine,
            _realm,
            arguments.At(0),
            arguments.At(1),
            "Failed to construct 'PerformanceMark'");

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.PerformanceMark.PrototypeObject,
            static (Engine engine, Realm _, (JsString Name, double StartTime, JsValue Detail) state)
                => new JsPerformanceMark(engine, state.Name, state.StartTime, state.Detail),
            (Name: name, StartTime: startTime, Detail: detail));
    }

    /// <summary>
    /// Steps 1 and 3 to 7 of the constructor, shared with <c>performance.mark()</c>, whose step 1 is "run the
    /// PerformanceMark constructor".
    /// </summary>
    /// <remarks>
    /// <para>
    /// Step 1 — the <c>SyntaxError</c> for a name that collides with a read-only <c>PerformanceTiming</c>
    /// attribute — is guarded by "if the current global object is a Window object", which is never true here,
    /// so it is deliberately absent. The same goes for the <c>convert a name to a timestamp</c> branch of
    /// <c>measure</c>: <c>PerformanceTiming</c> belongs to a document's navigation and there is none.
    /// </para>
    /// <para>
    /// The order matters and is the WebIDL binding's, not the algorithm's: the arguments are converted left to
    /// right, so <c>markName</c> is stringified and then the whole options dictionary is read — including its
    /// <c>detail</c>, whose getter therefore runs before the negative-<c>startTime</c> <c>TypeError</c> can be
    /// raised. The <c>detail</c> <i>clone</i> is step 7 and happens after that check, so a mark with a
    /// negative start time never serializes anything.
    /// </para>
    /// </remarks>
    internal static (JsString Name, double StartTime, JsValue Detail) ReadArguments(
        Engine engine,
        Realm realm,
        JsValue markName,
        JsValue markOptions,
        string context)
    {
        var name = TypeConverter.ToJsString(markName);
        var options = UserTiming.ReadMarkOptions(realm, markOptions, context);

        double startTime;
        if (options.HasStartTime)
        {
            // Step 5.2: "If markOptions's startTime is negative, throw a TypeError."
            if (options.StartTime < 0)
            {
                Throw.TypeError(realm, context + ": the 'startTime' value is negative.");
            }

            startTime = options.StartTime;
        }
        else
        {
            // Step 5.3: "Otherwise, set entry's startTime attribute to the value that would be returned by the
            // Performance object's now() method."
            startTime = UserTiming.RequireState(engine, "The PerformanceMark constructor").CurrentHighResolutionTime;
        }

        return (name, startTime, UserTiming.CloneDetail(engine, realm, options.Detail));
    }
}
#endif
