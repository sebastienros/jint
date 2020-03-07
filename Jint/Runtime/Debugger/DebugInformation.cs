using System;
using System.Collections.Generic;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Debugger
{
    public class DebugInformation : EventArgs
    {
        public Stack<string> CallStack { get; set; }
        public Statement CurrentStatement { get; set; }
        public long CurrentMemoryUsage { get; set; }
        public Dictionary<string, JsValue> Locals { get; set; }
        public Dictionary<string, JsValue> Globals { get; set; }
    }
}
