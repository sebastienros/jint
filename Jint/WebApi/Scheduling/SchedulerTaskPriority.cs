#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Runtime;

namespace Jint.WebApi.Scheduling;

/// <summary>
/// The <c>TaskPriority</c> enumeration,
/// https://wicg.github.io/scheduling-apis/#enumdef-taskpriority.
/// </summary>
/// <remarks>
/// The members are declared lowest first so that the numeric value <i>is</i> the priority order, which is what
/// <see cref="SchedulerTaskQueue.EffectivePriority"/> multiplies out into the specification's own table — see
/// https://wicg.github.io/scheduling-apis/#scheduler-task-queue-effective-priority.
/// </remarks>
internal enum SchedulerTaskPriority
{
    /// <summary>https://wicg.github.io/scheduling-apis/#dom-taskpriority-background — the lowest.</summary>
    Background = 0,

    /// <summary>
    /// https://wicg.github.io/scheduling-apis/#dom-taskpriority-user-visible — the default for a task that
    /// names no priority and carries no <c>TaskSignal</c>.
    /// </summary>
    UserVisible = 1,

    /// <summary>https://wicg.github.io/scheduling-apis/#dom-taskpriority-user-blocking — the highest.</summary>
    UserBlocking = 2,
}

/// <summary>
/// The three <c>TaskPriority</c> strings and the WebIDL enumeration conversion between them and
/// <see cref="SchedulerTaskPriority"/>.
/// </summary>
internal static class TaskPriorityNames
{
    internal static readonly JsString UserBlocking = new("user-blocking");
    internal static readonly JsString UserVisible = new("user-visible");
    internal static readonly JsString Background = new("background");

    /// <summary>
    /// The result of converting an IDL enumeration value to a JavaScript value: "the String value that
    /// represents the same sequence of code units as the enumeration value",
    /// https://webidl.spec.whatwg.org/#es-enumeration.
    /// </summary>
    internal static JsString ToJsString(SchedulerTaskPriority priority) => priority switch
    {
        SchedulerTaskPriority.UserBlocking => UserBlocking,
        SchedulerTaskPriority.Background => Background,
        _ => UserVisible,
    };

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-enumeration: <c>ToString</c> the value, and a string that is not one
    /// of the enumeration's values is a <c>TypeError</c> — never a silent fallback to the default.
    /// </summary>
    internal static SchedulerTaskPriority Parse(Realm realm, JsValue value, string what)
    {
        var text = TypeConverter.ToString(value);
        if (TryParse(text, out var priority))
        {
            return priority;
        }

        Throw.TypeError(
            realm,
            $"{what}: the provided value '{text}' is not a valid enum value of type TaskPriority.");
        return default;
    }

    private static bool TryParse(string text, out SchedulerTaskPriority priority)
    {
        switch (text)
        {
            case "user-blocking":
                priority = SchedulerTaskPriority.UserBlocking;
                return true;
            case "user-visible":
                priority = SchedulerTaskPriority.UserVisible;
                return true;
            case "background":
                priority = SchedulerTaskPriority.Background;
                return true;
            default:
                priority = default;
                return false;
        }
    }
}
#endif
