using System;
using Esprima;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Debugger
{
    public sealed class DebugInformation : EventArgs
    {
        internal DebugInformation(Statement currentStatement, DebugCallStack callStack, long currentMemoryUsage)
        {
            CurrentStatement = currentStatement;
            CallStack = callStack;
            CurrentMemoryUsage = currentMemoryUsage;
        }

        /// <summary>
        /// The current call stack.
        /// </summary>
        /// <remarks>This will always include at least a call frame for the global environment.</remarks>
        public DebugCallStack CallStack { get; set; }

        /// <summary>
        /// The Statement that will be executed on next step.
        /// Note that this will be null when execution is at a return point.
        /// </summary>
        public Statement CurrentStatement { get; }

        /// <summary>
        /// The current source Location.
        /// For return points, this starts and ends at the end of the function body.
        /// </summary>
        public Location Location => CurrentCallFrame.Location;

        /// <summary>
        /// Not implemented. Will always return 0.
        /// </summary>
        public long CurrentMemoryUsage { get; }

        /// <summary>
        /// The currently executing call frame.
        /// </summary>
        public CallFrame CurrentCallFrame => CallStack[0];

        /// <summary>
        /// The scope chain of the currently executing call frame.
        /// </summary>
        public DebugScopes CurrentScopeChain => CurrentCallFrame.ScopeChain;

        /// <summary>
        /// The return value of the currently executing call frame.
        /// This is null if execution is not at a return point. 
        /// </summary>
        public JsValue ReturnValue => CurrentCallFrame.ReturnValue;
    }
}
