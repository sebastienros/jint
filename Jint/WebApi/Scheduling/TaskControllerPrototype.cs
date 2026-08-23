#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Abort;

namespace Jint.WebApi.Scheduling;

/// <summary>
/// <c>TaskController.prototype</c> — the interface prototype object.
/// <para>
/// https://wicg.github.io/scheduling-apis/#taskcontroller
/// </para>
/// </summary>
/// <remarks>
/// Its <c>[[Prototype]]</c> is <c>AbortController.prototype</c>, which is where <c>signal</c> and
/// <c>abort()</c> come from; the only member declared here is <c>setPriority()</c>.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class TaskControllerPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly TaskControllerConstructor _constructor;

    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString TaskControllerToStringTag = new("TaskController");

    internal TaskControllerPrototype(
        Engine engine,
        Realm realm,
        TaskControllerConstructor constructor,
        ObjectInstance abortControllerPrototype) : base(engine, realm)
    {
        _prototype = abortControllerPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#dom-taskcontroller-setpriority — "the setPriority(priority)
    /// method steps are to signal priority change on this's signal given priority".
    /// </summary>
    /// <remarks>
    /// Everything that follows is that one algorithm: the tasks already queued against this signal are
    /// re-prioritized where they stand, then <c>prioritychange</c> is fired at the signal, then any signal
    /// whose priority follows this one is changed in turn. Setting the priority the signal already has does
    /// nothing at all, and calling this from inside a <c>prioritychange</c> listener is a
    /// <c>NotAllowedError</c>.
    /// </remarks>
    [JsFunction(Name = "setPriority", Length = 1, Flags = PropertyFlag.ConfigurableEnumerableWritable)]
    private JsValue SetPriority(JsValue thisObject, JsValue priority)
    {
        var signal = Brand(thisObject);
        signal.SignalPriorityChange(TaskPriorityNames.Parse(_realm, priority, "Failed to execute 'setPriority' on 'TaskController'"));
        return Undefined;
    }

    /// <summary>
    /// The WebIDL brand check: the receiver must be a <c>TaskController</c>, which here means a controller
    /// whose signal is a <see cref="JsTaskSignal"/> — a plain <c>AbortController</c> has no priority to change.
    /// </summary>
    private JsTaskSignal Brand(JsValue thisObject)
    {
        if (thisObject is JsAbortController { Signal: JsTaskSignal signal })
        {
            return signal;
        }

        Throw.TypeError(_realm, "Illegal invocation: receiver is not a TaskController");
        return null!;
    }
}
#endif
