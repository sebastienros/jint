
namespace Jint.Runtime
{
    internal static class ThreadSafety
    {
        public static bool ValidateThread(Engine engine)
        {
            if (!engine.Options.Interop.EnforceThreadConsistency)
            {
                return true;
            }
            var currentThread = System.Threading.Thread.CurrentThread;
            if (currentThread != engine.Thread)
            {
                return false;
            }
            return true;
        }
    }
}
