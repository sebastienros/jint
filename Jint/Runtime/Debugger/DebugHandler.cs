using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Native;
using Jint.Parser.Ast;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Debugger
{
    internal class DebugHandler
    {
        private readonly Stack<string> _debugCallStack;
        private StepMode _stepMode;
        private int _callBackStepOverDepth;
        private readonly Engine _engine;

        public DebugHandler(Engine engine)
        {
            _engine = engine;
            _debugCallStack = new Stack<string>();
            _stepMode = StepMode.Into;
        }

        internal void PopDebugCallStack()
        {
            if (_debugCallStack.Count > 0)
            {
                _debugCallStack.Pop();
            }
            if (_stepMode == StepMode.Over && _debugCallStack.Count < _callBackStepOverDepth)
            {
                _callBackStepOverDepth = _debugCallStack.Count;
                _stepMode = StepMode.Into;
            }
        }

        internal void AddToDebugCallStack(CallExpression callExpression)
        {
            var identifier = callExpression.Callee as Identifier;
            if (identifier != null)
            {
                var stack = identifier.Name + "(";
                var paramStrings = new List<string>();

                foreach (var argument in callExpression.Arguments)
                {
                    if (argument != null)
                    {
                        var argIdentifier = argument as Identifier;
                        paramStrings.Add(argIdentifier != null ? argIdentifier.Name : "null");
                    }
                    else
                    {
                        paramStrings.Add("null");
                    }
                }

                stack += string.Join(", ", paramStrings);
                stack += ")";
                _debugCallStack.Push(stack);
            }
        }

        internal void OnStep(Statement statement)
        {
            var old = _stepMode;
            if (statement == null)
            {
                return;
            }
            
            BreakPoint breakpoint = _engine.BreakPoints.FirstOrDefault(breakPoint => BpTest(statement, breakPoint));
            bool breakpointFound = false;

            if (breakpoint != null)
            {
                DebugInformation info = CreateDebugInformation(statement);
                var result = _engine.InvokeBreakEvent(info);
                if (result.HasValue)
                {
                    _stepMode = result.Value;
                    breakpointFound = true;
                }
            }

            if (breakpointFound == false && _stepMode == StepMode.Into)
            {
                DebugInformation info = CreateDebugInformation(statement);
                var result = _engine.InvokeStepEvent(info);
                if (result.HasValue)
                {
                    _stepMode = result.Value;
                }
            }

            if (old == StepMode.Into && _stepMode == StepMode.Over)
            {
                _callBackStepOverDepth = _debugCallStack.Count;
            }
        }

        private bool BpTest(Statement statement, BreakPoint breakpoint)
        {
            bool afterStart, beforeEnd;

            afterStart = (breakpoint.Line == statement.Location.Start.Line &&
                             breakpoint.Char >= statement.Location.Start.Column);

            if (!afterStart)
            {
                return false;
            }

            beforeEnd = breakpoint.Line < statement.Location.End.Line
                        || (breakpoint.Line == statement.Location.End.Line &&
                            breakpoint.Char <= statement.Location.End.Column);

            if (!beforeEnd)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(breakpoint.Condition))
            {
                return _engine.Execute(breakpoint.Condition).GetCompletionValue().AsBoolean();
            }

            return true;
        }

        private DebugInformation CreateDebugInformation(Statement statement)
        {
            var info = new DebugInformation { CurrentStatement = statement, CallStack = _debugCallStack };

            if (_engine.ExecutionContext != null && _engine.ExecutionContext.LexicalEnvironment != null)
            {
                var lexicalEnvironment = _engine.ExecutionContext.LexicalEnvironment;
                info.Locals = GetLocalVariables(lexicalEnvironment);
                info.Globals = GetGlobalVariables(lexicalEnvironment);
            }

            return info;
        }

        private static Dictionary<string, JsValue> GetLocalVariables(LexicalEnvironment lex)
        {
            Dictionary<string, JsValue> locals = new Dictionary<string, JsValue>();
            if (lex != null && lex.Record != null)
            {
                AddRecordsFromEnvironment(lex, locals);
            }
            return locals;
        }

        private static Dictionary<string, JsValue> GetGlobalVariables(LexicalEnvironment lex)
        {
            Dictionary<string, JsValue> globals = new Dictionary<string, JsValue>();
            LexicalEnvironment tempLex = lex;

            while (tempLex != null && tempLex.Record != null)
            {
                AddRecordsFromEnvironment(tempLex, globals);
                tempLex = tempLex.Outer;
            }
            return globals;
        }

        private static void AddRecordsFromEnvironment(LexicalEnvironment lex, Dictionary<string, JsValue> locals)
        {
            var bindings = lex.Record.GetAllBindingNames();
            foreach (var binding in bindings)
            {
                if (locals.ContainsKey(binding) == false)
                {
                    var jsValue = lex.Record.GetBindingValue(binding, false);
                    if (jsValue.TryCast<ICallable>() == null)
                    {
                        locals.Add(binding, jsValue);
                    }
                }
            }
        }
    }
}
