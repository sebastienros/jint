using System.Collections.Generic;
using System.Linq;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;

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

        internal void AddToDebugCallStack(JsValue function, CallExpression callExpression)
        {
            string name = GetCalleeName(function, callExpression.Callee);
            _debugCallStack.Push(name);
        }

        internal void PopDebugCallStack()
        {
            if (_debugCallStack.Count > 0)
            {
                _debugCallStack.Pop();
            }
            if (_stepMode == StepMode.Out && _debugCallStack.Count < _callBackStepOverDepth)
            {
                _callBackStepOverDepth = _debugCallStack.Count;
                _stepMode = StepMode.Into;
            }
            else if (_stepMode == StepMode.Over && _debugCallStack.Count == _callBackStepOverDepth)
            {
                _callBackStepOverDepth = _debugCallStack.Count;
                _stepMode = StepMode.Into;
            }
        }

        private string GetCalleeName(JsValue function, Expression calleeExpression)
        {
            switch (function)
            {
                case DelegateWrapper _:
                    return "(native code)";
                case FunctionInstance instance:
                    PropertyDescriptor nameDescriptor = instance.GetOwnProperty(CommonProperties.Name);
                    JsValue nameValue = nameDescriptor != null ? instance.UnwrapJsValue(nameDescriptor) : JsString.Empty;
                    return !nameValue.IsUndefined() ? TypeConverter.ToString(nameValue) : "(anonymous)";
                default:
                    return "(unknown)";
            }
        }

        internal void OnStep(Statement statement)
        {
            if (statement == null)
            {
                return;
            }

            var old = _stepMode;

            BreakPoint breakpoint = _engine.BreakPoints.FirstOrDefault(breakPoint => BpTest(statement, breakPoint));
            
            bool breakpointFound = false;
            if (breakpoint != null)
            {
                breakpointFound = Break(statement);
            }

            if (!breakpointFound && _stepMode == StepMode.Into)
            {
                DebugInformation info = CreateDebugInformation(statement);
                var result = _engine.InvokeStepEvent(info);
                if (result.HasValue)
                {
                    _stepMode = result.Value;
                }
            }

            if (old == StepMode.Into)
            {
                switch (_stepMode)
                {
                    case StepMode.Out:
                        _callBackStepOverDepth = _debugCallStack.Count;
                        break;
                    case StepMode.Over:
                        // Step over any statement that includes a CallExpression.
                        // TODO: This can certainly be improved - maybe make use of AddToDebugCallStack
                        if (statement.DescendantNodesAndSelf().Any(n => n is CallExpression))
                        {
                            _callBackStepOverDepth = _debugCallStack.Count;
                        }
                        else
                        {
                            _stepMode = StepMode.Into;
                        }
                        break;
                }
            }
        }

        internal bool Break(Statement statement)
        {
            DebugInformation info = CreateDebugInformation(statement);
            var result = _engine.InvokeBreakEvent(info);
            if (result.HasValue)
            {
                _stepMode = result.Value;
                return true;
            }
            return false;
        }

        private bool BpTest(Statement statement, BreakPoint breakpoint)
        {
            if (breakpoint.Source != null)
            {
                if (breakpoint.Source != statement.Location.Source)
                {
                    return false;
                }
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
                var completionValue = _engine.Execute(breakpoint.Condition).GetCompletionValue();
                return ((JsBoolean) completionValue)._value;
            }

            return true;
        }

        private DebugInformation CreateDebugInformation(Statement statement)
        {
            var info = new DebugInformation
            {
                CurrentStatement = statement,
                CallStack = _debugCallStack,
                CurrentMemoryUsage = _engine.CurrentMemoryUsage
            };

            info.Locals = GetLocalVariables(_engine.ExecutionContext);
            info.Globals = GetGlobalVariables(_engine.ExecutionContext);

            return info;
        }

        private static Dictionary<string, JsValue> GetLocalVariables(ExecutionContext context)
        {
            Dictionary<string, JsValue> locals = new Dictionary<string, JsValue>();

            // Local variables are the union of function scope (VariableEnvironment)
            // and any current block scope (LexicalEnvironment)
            if (!ReferenceEquals(context.VariableEnvironment?._record, null))
            {
                AddRecordsFromEnvironment(context.VariableEnvironment, locals);
            }
            if (!ReferenceEquals(context.LexicalEnvironment?._record, null))
            {
                AddRecordsFromEnvironment(context.LexicalEnvironment, locals);
            }
            return locals;
        }

        private static Dictionary<string, JsValue> GetGlobalVariables(ExecutionContext context)
        {
            Dictionary<string, JsValue> globals = new Dictionary<string, JsValue>();
            
            // Unless we're in the global scope (_outer is null), don't include function local variables.
            // The function local variables are in the variable environment (function scope) and any current
            // lexical environment (block scope), which will be a "child" of that VariableEnvironment.
            // Hence, we should only use the VariableEnvironment's outer environment for global scope. This
            // also means that block scoped variables will never be included - they'll be listed as local variables.
            LexicalEnvironment tempLex = context.VariableEnvironment._outer ?? context.VariableEnvironment;

            while (!ReferenceEquals(tempLex?._record, null))
            {
                AddRecordsFromEnvironment(tempLex, globals);
                tempLex = tempLex._outer;
            }
            return globals;
        }

        private static void AddRecordsFromEnvironment(LexicalEnvironment lex, Dictionary<string, JsValue> locals)
        {
            var bindings = lex._record.GetAllBindingNames();
            foreach (var binding in bindings)
            {
                if (!locals.ContainsKey(binding))
                {
                    var jsValue = lex._record.GetBindingValue(binding, false);

                    switch (jsValue)
                    {
                        case ICallable _:
                            // TODO: Callables aren't added - but maybe they should be.
                            break;
                        case null:
                            // Uninitialized consts in scope are shown as "undefined" in e.g. Chromium debugger.
                            // Uninitialized lets aren't displayed.
                            // TODO: Check if null result from GetBindingValue is only true for uninitialized const/let.
                            break;
                        default:
                            locals.Add(binding, jsValue);
                            break;
                    }
                }
            }
        }
    }
}
