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
        private int _steppingDepth;
        private readonly Engine _engine;

        public DebugHandler(Engine engine)
        {
            _engine = engine;
            _debugCallStack = new Stack<string>();
            _steppingDepth = int.MaxValue;
        }

        internal void AddToDebugCallStack(JsValue function)
        {
            string name = GetCalleeName(function);

            _debugCallStack.Push(name);
        }

        internal void PopDebugCallStack()
        {
            if (_debugCallStack.Count > 0)
            {
                _debugCallStack.Pop();
            }
        }

        private string GetCalleeName(JsValue function)
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
            BreakPoint breakpoint = _engine.BreakPoints.FirstOrDefault(breakPoint => BpTest(statement, breakPoint));

            if (breakpoint != null)
            {
                Break(statement);
            }
            else if (_debugCallStack.Count <= _steppingDepth)
            {
                Step(statement);
            }
        }

        private void Step(Statement statement)
        {
            DebugInformation info = CreateDebugInformation(statement);
            StepMode? result = _engine.InvokeStepEvent(info);
            HandleNewStepMode(result);
        }

        internal void Break(Statement statement)
        {
            DebugInformation info = CreateDebugInformation(statement);
            StepMode? result = _engine.InvokeBreakEvent(info);
            HandleNewStepMode(result);
        }

        private void HandleNewStepMode(StepMode? newStepMode)
        {
            if (newStepMode != null)
            {
                switch (newStepMode)
                {
                    case StepMode.Over:
                        // Resume stepping when we're back at this level of the call stack
                        _steppingDepth = _debugCallStack.Count;
                        break;
                    case StepMode.Out:
                        // Resume stepping when we've popped the call stack
                        _steppingDepth = _debugCallStack.Count - 1;
                        break;
                    case StepMode.None:
                        // Never step
                        _steppingDepth = int.MinValue;
                        break;
                    default:
                        // Always step
                        _steppingDepth = int.MaxValue;
                        break;
                }
            }
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
