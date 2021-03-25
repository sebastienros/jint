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
        /// The Statement that will be executed on next step. Note that this will be null when at a return point.
        /// </summary>
        public Statement CurrentStatement { get; set; }

        /// <summary>
        /// The current source Location. For return points, this starts and ends at the end of the function body.
        /// </summary>
        public Location Location { get; set; }

        public long CurrentMemoryUsage { get; set; }

        /// <summary>
        /// The current scope chain.
        /// </summary>
        public DebugScopes Scopes { get; set; }

        /// <summary>
        /// The return value. This is null if we're not at a return point. 
        /// </summary>
        public JsValue ReturnValue { get; set; }
    }
}
