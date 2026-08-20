#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Scheduling;

/// <summary>
/// <c>TaskPriorityChangeEvent.prototype</c> — the interface prototype object.
/// <para>
/// https://wicg.github.io/scheduling-apis/#taskprioritychangeevent
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>Event.prototype</c>, which is what gives the event <c>type</c>,
/// <c>target</c>, <c>timeStamp</c> and the rest, and makes
/// <c>new TaskPriorityChangeEvent(…) instanceof Event</c> hold.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class TaskPriorityChangeEventPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly TaskPriorityChangeEventConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString TaskPriorityChangeEventToStringTag = new("TaskPriorityChangeEvent");

    internal TaskPriorityChangeEventPrototype(
        Engine engine,
        Realm realm,
        TaskPriorityChangeEventConstructor constructor,
        ObjectInstance eventPrototype) : base(engine, realm)
    {
        _prototype = eventPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#dom-taskprioritychangeevent-previouspriority — "the
    /// previousPriority getter steps are to return the value that the corresponding attribute was initialized
    /// to".
    /// </summary>
    [JsAccessor("previousPriority", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString PreviousPriorityGet(JsValue thisObject)
    {
        if (thisObject is JsTaskPriorityChangeEvent priorityChange)
        {
            return TaskPriorityNames.ToJsString(priorityChange.PreviousPriority);
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a TaskPriorityChangeEvent");
        return null!;
    }
}
#endif
