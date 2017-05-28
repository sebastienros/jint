using System;
using System.Collections.Generic;
using Jint.Native;
using Jint.Parser.Ast;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Debugger
{
    public class DebugInformation : EventArgs
    {
        public DebugInformation(StackFrame[] callStack, Statement currentStatement, Dictionary<string, JsValue> locals, Dictionary<string, JsValue> globals, BreakPoint breakPoint, Engine engine, Program program, JavaScriptException exception, bool uncaughtException, StepMode stepMode)
        {
            CallStack = callStack;
            CurrentStatement = currentStatement;
            Locals = locals;
            Globals = globals;
            BreakPoint = breakPoint;
            Engine = engine;
            Program = program;
            Exception = exception;
            UncaughtException = uncaughtException;
            StepMode = stepMode;
        }

        public StackFrame[] CallStack { get; }
        public Statement CurrentStatement { get; }
        public Dictionary<string, JsValue> Locals { get; }
        public Dictionary<string, JsValue> Globals { get; }
        public BreakPoint BreakPoint { get; }
        public Engine Engine { get; }
        public Program Program { get; }
        public JavaScriptException Exception { get; }
        public bool UncaughtException { get; }
        public StepMode StepMode { get; }
    }
}
