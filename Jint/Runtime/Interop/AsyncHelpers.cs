using System.Linq;
using System.Threading.Tasks;

namespace Jint.Runtime.Interop
{
    public static class AsyncHelpers
    {
        // The idea of this helper was to avoid async constructs in javascript. So this one just auto awaits on any task type.
        // I realize it might be better to just return the Task and handle the await in JS to give more control to the developer (cancellation and timeout)
        // However, it would also be useful in many cases to just have this done automatically by the helper, to keep the JS code clean - so maybe it could be an optional feature w/timeout.
        // I'm leaving it here, as this is what I use at the moment

        public async static Task<object> AwaitWhenAsyncResult(this object callResult)
        {
            if (!(callResult is Task task)) return callResult;

            await task;

            // Return the result, if the task has one (must be a generic Task that is not of type VoidTaskResult)
            return TaskHasResult(task)
                ? (object)((dynamic)task)?.Result
                : null;
        }

        private static bool TaskHasResult(Task task)
        {
            // VoidTaskResult is an internal Microsoft class used as Task<VoidTaskResult> which correlates to the standard non generic Task
            var taskType = task.GetType();
            return taskType.IsGenericType
                && taskType.GenericTypeArguments[0].Name != "VoidTaskResult";
        }
    }
}