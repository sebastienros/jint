#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Abort;

namespace Jint.WebApi.Scheduling;

/// <summary>
/// The <c>TaskSignal</c> interface object.
/// <para>
/// https://wicg.github.io/scheduling-apis/#tasksignal
/// </para>
/// </summary>
/// <remarks>
/// <c>TaskSignal</c> inherits from <c>AbortSignal</c>, so its <c>[[Prototype]]</c> is the <c>AbortSignal</c>
/// interface object — https://webidl.spec.whatwg.org/#interface-object. It declares no constructor operation,
/// which in WebIDL means the interface object is a function that refuses to construct anything, so a task
/// signal can only come from a <c>TaskController</c> or from <see cref="StaticAny"/>.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class TaskSignalConstructor : Constructor
{
    private static readonly JsString _functionName = new("TaskSignal");
    private static readonly JsString _priorityProperty = new("priority");

    internal TaskSignalConstructor(Engine engine, Realm realm, AbortSignalConstructor abortSignalConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = abortSignalConstructor;
        PrototypeObject = new TaskSignalPrototype(engine, realm, this, abortSignalConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal TaskSignalPrototype PrototypeObject { get; }

    protected override void Initialize() => CreateProperties_Generated();

    /// <summary>
    /// An interface without a constructor operation is not constructible.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }

    /// <summary>
    /// Builds a fresh, non-aborted task signal in this realm at the given priority. Used by
    /// <see cref="TaskControllerConstructor"/>.
    /// </summary>
    internal JsTaskSignal CreateSignal(SchedulerTaskPriority priority)
    {
        return new JsTaskSignal(_engine, _realm, priority)
        {
            _prototype = PrototypeObject,
        };
    }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#dom-tasksignal-any — a signal aborted by whichever of
    /// <paramref name="signals"/> aborts first, whose priority is either fixed or follows another task signal.
    /// </summary>
    /// <remarks>
    /// Shadowing the inherited <c>AbortSignal.any</c> is the point: without it a script calling
    /// <c>TaskSignal.any(…)</c> would reach the parent interface's static through the interface object's
    /// prototype chain and get back a plain <c>AbortSignal</c>, which has no priority at all.
    /// </remarks>
    [JsFunction(Name = "any", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsTaskSignal StaticAny(JsValue thisObject, JsValue signals, JsValue init)
    {
        var sources = _realm.Intrinsics.AbortSignal.ReadSignalSequence(signals, "TaskSignal");
        var (priority, prioritySource) = ReadAnyInit(init);
        return JsTaskSignal.CreateDependent(_engine, _realm, sources, priority, prioritySource);
    }

    /// <summary>
    /// The <c>TaskSignalAnyInit</c> dictionary, https://wicg.github.io/scheduling-apis/#dictdef-tasksignalanyinit,
    /// whose one member is the union <c>(TaskPriority or TaskSignal)</c> defaulting to <c>"user-visible"</c>.
    /// </summary>
    /// <remarks>
    /// https://webidl.spec.whatwg.org/#es-union sends a platform object implementing one of the union's
    /// interface types down that branch, and everything else down the enumeration one — so a <c>TaskSignal</c>
    /// is the priority source and any other value is stringified and matched against the three priority names.
    /// </remarks>
    private (SchedulerTaskPriority Priority, JsTaskSignal? Source) ReadAnyInit(JsValue init)
    {
        if (init.IsUndefined() || init.IsNull())
        {
            return (SchedulerTaskPriority.UserVisible, null);
        }

        if (init is not ObjectInstance dictionary)
        {
            Throw.TypeError(_realm, "Failed to execute 'any' on 'TaskSignal': the provided value is not of type 'TaskSignalAnyInit'.");
            return default;
        }

        var priority = dictionary.Get(_priorityProperty);
        if (priority.IsUndefined())
        {
            return (SchedulerTaskPriority.UserVisible, null);
        }

        if (priority is JsTaskSignal source)
        {
            return (source.Priority, source);
        }

        return (TaskPriorityNames.Parse(_realm, priority, "Failed to execute 'any' on 'TaskSignal'"), null);
    }
}
#endif
