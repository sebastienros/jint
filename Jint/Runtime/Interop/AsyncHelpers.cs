using System.Linq;
using System.Threading.Tasks;

namespace Jint.Runtime.Interop
{
    public static class AsyncHelpers
    {
        public async static Task<object> AwaitWhenAsyncResult(this object callResult)
        {
            var task = callResult as Task;
            if (task == null) return callResult; //Not a Task

            await task;

            // Return the result, unless it's a VoidTaskResult
            return IsVoidTaskResult(task)
                ? null
                : (object)((dynamic)task)?.Result;
        }

        private static bool IsVoidTaskResult(Task task)
        {
            // VoidTaskResult is an internal Microsoft class that is used as the generic type of a generic task, to indicate it has no result
            // Task<VoidTaskResult> correlates to the standard non generic Task
            var taskType = task.GetType();
            return taskType.IsGenericType
                && taskType.GenericTypeArguments[0].Name == "VoidTaskResult";
        }
    }
}