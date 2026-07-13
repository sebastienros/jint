using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Interop;

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

        /// <summary>
        /// Creates an ECMAScript Proxy exotic object whose traps are implemented in .NET by
        /// <paramref name="handler"/>. This is the CLR-side equivalent of <c>new Proxy(target, handlerObject)</c>
        /// in script, which remains the way to create proxies with JavaScript handler objects.
        /// </summary>
        /// <remarks>
        /// A trap method returning CLR <see langword="null"/> forwards the operation to
        /// <paramref name="target"/>, exactly like an absent trap on a JavaScript handler object.
        /// All ECMAScript proxy invariants are enforced on trap results. Note that <c>proxy.method(x)</c>
        /// fires the <see cref="ProxyHandler.Get"/> trap (returning the method) followed by a plain call
        /// on the result — the <see cref="ProxyHandler.Apply"/> trap only fires when the proxy itself is
        /// invoked, so intercepting method calls means returning a wrapping function from
        /// <see cref="ProxyHandler.Get"/>.
        /// </remarks>
        /// <param name="target">The proxy target object.</param>
        /// <param name="handler">The .NET trap implementation.</param>
        /// <returns>The proxy object, ready to be passed into script.</returns>
        public ObjectInstance CreateProxy(ObjectInstance target, ProxyHandler handler)
        {
            if (target is null)
            {
                Throw.ArgumentNullException(nameof(target));
            }

            if (handler is null)
            {
                Throw.ArgumentNullException(nameof(handler));
            }

            return new JsProxy(_engine, target, handler);
        }

        /// <summary>
        /// Creates a revocable ECMAScript Proxy exotic object whose traps are implemented in .NET by
        /// <paramref name="handler"/>, mirroring JavaScript's <c>Proxy.revocable()</c>. See
        /// <see cref="CreateProxy"/> for trap forwarding and invariant semantics.
        /// </summary>
        /// <param name="target">The proxy target object.</param>
        /// <param name="handler">The .NET trap implementation.</param>
        /// <returns>The proxy paired with its revoke operation.</returns>
        public RevocableProxy CreateRevocableProxy(ObjectInstance target, ProxyHandler handler)
        {
            if (target is null)
            {
                Throw.ArgumentNullException(nameof(target));
            }

            if (handler is null)
            {
                Throw.ArgumentNullException(nameof(handler));
            }

            return new RevocableProxy(new JsProxy(_engine, target, handler));
        }

        /// <summary>
        /// Event raised when a promise is rejected without a handler (operation = Reject),
        /// or when a handler is added to a previously unhandled rejected promise (operation = Handle).
        /// This implements the HostPromiseRejectionTracker abstract operation from the TC39 spec.
        /// </summary>
        /// <remarks>
        /// https://tc39.es/ecma262/#sec-hostpromiserejectiontracker
        /// </remarks>
        public event EventHandler<PromiseRejectionTrackerEventArgs>? PromiseRejectionTracker;

        internal void RaisePromiseRejectionTracker(JsPromise promise, PromiseRejectionOperation operation)
        {
            PromiseRejectionTracker?.Invoke(_engine, new PromiseRejectionTrackerEventArgs(promise, operation));
        }
    }
}

/// <summary>
/// Event arguments for the PromiseRejectionTracker event.
/// </summary>
public sealed class PromiseRejectionTrackerEventArgs : EventArgs
{
    /// <summary>
    /// The promise that triggered the rejection tracking.
    /// </summary>
    public JsValue Promise { get; }

    /// <summary>
    /// The rejection reason (only meaningful when Operation is Reject).
    /// </summary>
    public JsValue? Value { get; }

    /// <summary>
    /// Whether this is a new unhandled rejection ("Reject") or a previously
    /// unhandled rejection that now has a handler ("Handle").
    /// </summary>
    public PromiseRejectionOperation Operation { get; }

    internal PromiseRejectionTrackerEventArgs(JsPromise promise, PromiseRejectionOperation operation)
    {
        Promise = promise;
        Value = promise.State == PromiseState.Rejected ? promise.Value : null;
        Operation = operation;
    }
}
