#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Events;

namespace Jint.WebApi.Scheduling;

/// <summary>
/// The <c>TaskPriorityChangeEvent</c> interface object.
/// <para>
/// https://wicg.github.io/scheduling-apis/#taskprioritychangeevent
/// </para>
/// </summary>
/// <remarks>
/// <c>TaskPriorityChangeEvent</c> inherits from <c>Event</c>, so its <c>[[Prototype]]</c> is the <c>Event</c>
/// interface object — https://webidl.spec.whatwg.org/#interface-object. Unlike every other event constructor
/// here its dictionary argument is <b>not</b> optional and its <c>previousPriority</c> member is
/// <c>required</c>, so <c>new TaskPriorityChangeEvent('prioritychange')</c> is a <c>TypeError</c>.
/// </remarks>
internal sealed class TaskPriorityChangeEventConstructor : Constructor
{
    private static readonly JsString _functionName = new("TaskPriorityChangeEvent");
    private static readonly JsString _previousPriorityProperty = new("previousPriority");
    private static readonly JsString _priorityChangeEventName = new(JsTaskSignal.PriorityChangeEventType);

    internal TaskPriorityChangeEventConstructor(Engine engine, Realm realm, EventConstructor eventConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = eventConstructor;
        PrototypeObject = new TaskPriorityChangeEventPrototype(engine, realm, this, eventConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.Create(2), PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal TaskPriorityChangeEventPrototype PrototypeObject { get; }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#dom-taskprioritychangeevent-taskprioritychangeevent
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var type = EventConstructor.RequireType(_realm, arguments, "TaskPriorityChangeEvent");

        if (arguments.Length < 2)
        {
            Throw.TypeError(_realm, "Failed to construct 'TaskPriorityChangeEvent': 2 arguments required, but only 1 present.");
        }

        // The inherited dictionary's members are converted before the interface's own —
        // https://webidl.spec.whatwg.org/#es-dictionary, "for each dictionary dictionary in dictionaries, in
        // order".
        var initArgument = arguments.At(1);
        var init = EventConstructor.ReadEventInit(_realm, initArgument, "TaskPriorityChangeEvent");
        var previousPriority = ReadPreviousPriority(initArgument);

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.TaskPriorityChangeEvent.PrototypeObject,
            static (Engine engine, Realm _, (JsString Type, EventInit Init, double TimeStamp, SchedulerTaskPriority Previous) state)
                => new JsTaskPriorityChangeEvent(engine, state.Type, state.Init, state.TimeStamp, state.Previous),
            (Type: type, Init: init, TimeStamp: EventConstructor.TimeStampNow(_engine), Previous: previousPriority));
    }

    /// <summary>
    /// https://dom.spec.whatwg.org/#concept-event-fire step 2 for the <c>prioritychange</c> event: an event
    /// the engine creates for itself, so <c>isTrusted</c> is true. Not reachable from script.
    /// </summary>
    internal JsTaskPriorityChangeEvent CreateTrustedEvent(SchedulerTaskPriority previousPriority)
    {
        return new JsTaskPriorityChangeEvent(
            _engine,
            _priorityChangeEventName,
            default,
            EventConstructor.TimeStampNow(_engine),
            previousPriority)
        {
            IsTrusted = true,
            _prototype = PrototypeObject,
        };
    }

    /// <summary>
    /// The <c>previousPriority</c> member of
    /// https://wicg.github.io/scheduling-apis/#dictdef-taskprioritychangeeventinit, which is <c>required</c>:
    /// an absent member — or an explicit <see langword="undefined"/>, which
    /// https://webidl.spec.whatwg.org/#es-dictionary treats as absent — is a <c>TypeError</c> rather than a
    /// default.
    /// </summary>
    private SchedulerTaskPriority ReadPreviousPriority(JsValue init)
    {
        const string What = "Failed to construct 'TaskPriorityChangeEvent'";

        if (init is not ObjectInstance dictionary)
        {
            // A non-object init already raised the TypeError above; null and undefined reach here, and a
            // required member cannot be defaulted.
            Throw.TypeError(_realm, $"{What}: required member previousPriority is undefined.");
            return default;
        }

        var previousPriority = dictionary.Get(_previousPriorityProperty);
        if (previousPriority.IsUndefined())
        {
            Throw.TypeError(_realm, $"{What}: required member previousPriority is undefined.");
        }

        return TaskPriorityNames.Parse(_realm, previousPriority, What);
    }
}

/// <summary>
/// A <c>TaskPriorityChangeEvent</c> instance: an <see cref="JsEvent"/> that also carries the priority its
/// target had before the change.
/// <para>
/// https://wicg.github.io/scheduling-apis/#taskprioritychangeevent
/// </para>
/// </summary>
internal sealed class JsTaskPriorityChangeEvent : JsEvent
{
    internal JsTaskPriorityChangeEvent(
        Engine engine,
        JsString type,
        EventInit init,
        double timeStamp,
        SchedulerTaskPriority previousPriority)
        : base(engine, type, init, timeStamp)
    {
        PreviousPriority = previousPriority;
    }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#dom-taskprioritychangeevent-previouspriority. The <i>new</i>
    /// priority is not on the event at all — it is read from <c>event.target.priority</c>.
    /// </summary>
    internal SchedulerTaskPriority PreviousPriority { get; }
}
#endif
