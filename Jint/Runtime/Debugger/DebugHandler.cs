using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Jint.Native;
using Jint.Native.Function;
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
        private bool _callPending;

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
            if (
                (_stepMode == StepMode.Out && _debugCallStack.Count < _callBackStepOverDepth) ||
                (_stepMode == StepMode.Over && (_callPending || _debugCallStack.Count == _callBackStepOverDepth))
            ) {
                _callBackStepOverDepth = _debugCallStack.Count;
                _stepMode = StepMode.Into;
            }
        }

        internal void AddToDebugCallStack(CallExpression callExpression, object callee)
        {
            if (_callPending)
            {
                _callPending = false;
                _callBackStepOverDepth = _debugCallStack.Count;
            }

            string identifier = (callExpression.Callee as Identifier)?.Name;
            if (identifier == null)
            {
                var functionExpression = callExpression.Callee as FunctionExpression;
                if (functionExpression != null)
                    identifier = functionExpression.Id?.Name ?? "(anonymous function)";
            }
            if (identifier == null)
            {
                var memberExpression = callExpression.Callee as MemberExpression;
                if (memberExpression != null)
                    identifier = (memberExpression.Property as Identifier)?.Name ?? "(anonymous function)";
            }
            if (identifier == null)
                identifier = "(anonymous function)";

            var stack = new StringBuilder()
                .Append(identifier)
                .Append('(');

            bool hadOne = false;

            // Try to resolve the function to get the formal parameters.

            FunctionInstance func;

            try
            {
                func = _engine.GetValue(callee).TryCast<FunctionInstance>();
            }
            catch
            {
                func = null;
            }

            if (func != null && func.FormalParameters != null)
            {
                foreach (var parameter in func.FormalParameters)
                {
                    if (hadOne)
                        stack.Append(", ");
                    else
                        hadOne = true;

                    stack.Append(parameter);
                }
            }

            _debugCallStack.Push(stack.Append(')').ToString());
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

            if (_callPending)
            {
                _stepMode = StepMode.Into;
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

            if (old == StepMode.Into && _stepMode == StepMode.Out)
            {
                _callBackStepOverDepth = _debugCallStack.Count;
            }
            else if (old == StepMode.Into && _stepMode == StepMode.Over)
            {
                _callPending = true;
            }
        }

        private bool BpTest(Statement statement, BreakPoint breakpoint)
        {
            if (statement.Location.Source != breakpoint.Source)
            {
                return false;
            }

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

        public DebugInformation CreateDebugInformation(Statement statement)
        {
            if (!_engine.Options._IsDebugMode)
                return null;

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
