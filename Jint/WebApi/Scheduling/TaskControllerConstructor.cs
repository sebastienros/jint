#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.WebApi.Abort;

namespace Jint.WebApi.Scheduling;

/// <summary>
/// The <c>TaskController</c> interface object.
/// <para>
/// https://wicg.github.io/scheduling-apis/#taskcontroller
/// </para>
/// </summary>
/// <remarks>
/// <c>TaskController</c> inherits from <c>AbortController</c>, so its <c>[[Prototype]]</c> is the
/// <c>AbortController</c> interface object and a task controller aborts exactly the way an abort controller
/// does — the only thing it adds is <c>setPriority()</c>. It declares no static member, so it needs nothing
/// from the source generator.
/// </remarks>
internal sealed class TaskControllerConstructor : Constructor
{
    private static readonly JsString _functionName = new("TaskController");
    private static readonly JsString _priorityProperty = new("priority");

    private readonly TaskSignalConstructor _signalConstructor;

    internal TaskControllerConstructor(
        Engine engine,
        Realm realm,
        AbortControllerConstructor abortControllerConstructor,
        TaskSignalConstructor signalConstructor)
        : base(engine, realm, _functionName)
    {
        _prototype = abortControllerConstructor;
        _signalConstructor = signalConstructor;
        PrototypeObject = new TaskControllerPrototype(engine, realm, this, abortControllerConstructor.PrototypeObject);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal TaskControllerPrototype PrototypeObject { get; }

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#dom-taskcontroller-taskcontroller — "let signal be a new
    /// TaskSignal object; set signal's priority to init["priority"]; set this's signal to signal".
    /// </summary>
    /// <remarks>
    /// The instance is a <see cref="JsAbortController"/> like any other controller: a task controller adds no
    /// state of its own, only a signal that happens to be a <see cref="JsTaskSignal"/>. That is also the brand
    /// <c>setPriority()</c> checks, so calling it on a plain <c>AbortController</c> is the <c>TypeError</c>
    /// WebIDL asks for.
    /// </remarks>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var priority = ReadInit(arguments.At(0));

        // The signal is built before the controller and handed to the creator, so that
        // `class C extends TaskController {}` still gets a plain TaskSignal — the specification's "let signal
        // be a new TaskSignal object" says nothing about the subclass.
        var signal = _signalConstructor.CreateSignal(priority);

        return OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.TaskController.PrototypeObject,
            static (Engine engine, Realm _, JsTaskSignal? state) => new JsAbortController(engine, state!),
            signal);
    }

    /// <summary>
    /// The <c>TaskControllerInit</c> dictionary,
    /// https://wicg.github.io/scheduling-apis/#dictdef-taskcontrollerinit, whose one member defaults to
    /// <c>"user-visible"</c>. An absent dictionary, an absent member and an explicit <see langword="undefined"/>
    /// all take that default, per https://webidl.spec.whatwg.org/#es-dictionary.
    /// </summary>
    private SchedulerTaskPriority ReadInit(JsValue init)
    {
        if (init.IsUndefined() || init.IsNull())
        {
            return SchedulerTaskPriority.UserVisible;
        }

        if (init is not ObjectInstance dictionary)
        {
            Throw.TypeError(_realm, "Failed to construct 'TaskController': the provided value is not of type 'TaskControllerInit'.");
            return default;
        }

        var priority = dictionary.Get(_priorityProperty);
        if (priority.IsUndefined())
        {
            return SchedulerTaskPriority.UserVisible;
        }

        return TaskPriorityNames.Parse(_realm, priority, "Failed to construct 'TaskController'");
    }
}
#endif
