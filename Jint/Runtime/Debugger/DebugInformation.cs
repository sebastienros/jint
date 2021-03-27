using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Esprima;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Debugger
{
    public class DebugInformation : EventArgs
    {
        public DebugCallStack CallStack { get; set; }

        /// <summary>
        /// The Statement that will be executed on next step.
        /// Note that this will be null when execution is at a return point.
        /// </summary>
        public Statement CurrentStatement { get; set; }

        /// <summary>
        /// The current source Location.
        /// For return points, this starts and ends at the end of the function body.
        /// </summary>
        public Location Location => CurrentCallFrame.Location;

        public long CurrentMemoryUsage { get; set; }

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
