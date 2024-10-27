using Jint.Native.Promise;

namespace Jint;

public partial class Engine
{
    public AdvancedOperations Advanced { get; }

    public class AdvancedOperations
    {
        private readonly Engine _engine;

        internal AdvancedOperations(Engine engine)
        {
            _engine = engine;
        }

        /// <summary>
        /// Gets current stack trace that is active in engine.
        /// </summary>
        public string StackTrace
        {
            get
            {
                var lastSyntaxElement = _engine._lastSyntaxElement;
                if (lastSyntaxElement is null)
                {
                    return string.Empty;
                }

                return _engine.CallStack.BuildCallStackString(_engine, lastSyntaxElement.Location);
            }
        }

        /// <summary>
        /// Initializes list of references of called functions
        /// </summary>
        public void ResetCallStack()
        {
            _engine.ResetCallStack();
        }

        /// <summary>
        /// Forcefully processes the current task queues (micro and regular), this API may break and change behavior!
        /// </summary>
        public void ProcessTasks()
        {
            _engine.RunAvailableContinuations();
        }

        /// <summary>
        /// EXPERIMENTAL! Subject to change.
        ///
        /// Registers a promise within the currently running EventLoop (has to be called within "ExecuteWithEventLoop" call).
        /// Note that ExecuteWithEventLoop will not trigger "onFinished" callback until ALL manual promises are settled.
        ///
        /// NOTE: that resolve and reject need to be called withing the same thread as "ExecuteWithEventLoop".
        /// The API assumes that the Engine is called from a single thread.
        /// </summary>
        /// <returns>a Promise instance and functions to either resolve or reject it</returns>
        public ManualPromise RegisterPromise()
        {
            return _engine.RegisterPromise();
        }
    }
}
