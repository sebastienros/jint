using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Parser.Ast;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Debugger
{
    public class DebugInformation : EventArgs
    {
        public Stack<String> CallStack { get; set; }
        public Statement CurrentStatement { get; set; }
        public Dictionary<string, JsValue> Locals { get; set; }
        public Dictionary<string, JsValue> Globals { get; set; }
    }
}
