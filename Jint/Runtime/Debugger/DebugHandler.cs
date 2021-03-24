using System;
using System.Collections.Generic;
using System.Linq;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Debugger
{
    internal class DebugHandler
    {
        private enum PauseType
        {
            Step,
            Break
        }

        private bool _paused;
        private int _steppingDepth;
        private readonly Engine _engine;

        public DebugHandler(Engine engine)
        {
            _engine = engine;
            _steppingDepth = int.MaxValue;
        }

        internal void OnStep(Statement statement)
        {
            // Don't reenter if we're already paused (e.g. when evaluating a getter in a Break/Step handler)
            if (_paused)
            {
                return;
            }

            BreakPoint breakpoint = _engine.BreakPoints.FirstOrDefault(breakPoint => BpTest(statement, breakPoint));

            if (breakpoint != null)
            {
                Pause(statement, PauseType.Break);
            }
            else if (_engine.CallStack.Count <= _steppingDepth)
            {
                Pause(statement, PauseType.Step);
            }
        }

        private void Pause(Statement statement, PauseType type)
        {
            _paused = true;
            
            DebugInformation info = CreateDebugInformation(statement);
            StepMode? result = type switch
            {
                PauseType.Step => _engine.InvokeStepEvent(info),
                PauseType.Break => _engine.InvokeBreakEvent(info),
                _ => throw new ArgumentException("Invalid pause type", nameof(type))
            };
            
            _paused = false;
            
            HandleNewStepMode(result);
        }

        internal void Break(Statement statement)
        {
            // Don't reenter if we're already paused
            if (_paused)
            {
                return;
            }

            Pause(statement, PauseType.Break);
        }

        private void HandleNewStepMode(StepMode? newStepMode)
        {
            if (newStepMode != null)
            {
                switch (newStepMode)
                {
                    case StepMode.Over:
                        // Resume stepping when we're back at this level of the call stack
                        _steppingDepth = _engine.CallStack.Count;
                        break;
                    case StepMode.Out:
                        // Resume stepping when we've popped the call stack
                        _steppingDepth = _engine.CallStack.Count - 1;
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
                CallStack = new DebugCallStack(statement.Location, _engine.CallStack),
                CurrentMemoryUsage = _engine.CurrentMemoryUsage,
                Scopes = new DebugScopes()
            };

            PopulateScopes(info.Scopes, _engine.ExecutionContext);

            return info;
        }

        private void PopulateScopes(DebugScopes scopes, ExecutionContext context)
        {
            var lexEnv = context.LexicalEnvironment;
            var varEnv = context.VariableEnvironment;
            HashSet<string> foundBindings = new HashSet<string>();
            while (!ReferenceEquals(lexEnv?._record, null))
            {
                PopulateScopesFromLexicalEnvironment(scopes, lexEnv, foundBindings);
                lexEnv = lexEnv._outer;
            }
        }

        private static void PopulateScopesFromLexicalEnvironment(DebugScopes scopes, LexicalEnvironment env, HashSet<string> foundBindings)
        {
            var bindings = GetBindings(env, foundBindings);
            if (bindings.Count > 0)
            {
                switch (env._record)
                {
                    case GlobalEnvironmentRecord:
                        scopes.Add(new DebugScope(DebugScopeType.Global, bindings));
                        break;
                    case FunctionEnvironmentRecord:
                        scopes.Add(new DebugScope(DebugScopeType.Local, bindings));
                        break;
                    case ObjectEnvironmentRecord:
                        // If an ObjectEnvironmentRecord is not a GlobalEnvironmentRecords, it's With
                        scopes.Add(new DebugScope(DebugScopeType.With, bindings));
                        break;
                    case DeclarativeEnvironmentRecord der:
                        scopes.Add(new DebugScope(
                            der._catchEnvironment ? DebugScopeType.Catch : DebugScopeType.Block,
                            bindings
                        ));
                        break;
                }
            }
        }

        private static IReadOnlyDictionary<string, JsValue> GetBindings(LexicalEnvironment lex, HashSet<string> foundBindings)
        {
            var bindings = lex._record.GetAllBindingNames();
            var result = new Dictionary<string, JsValue>();
            
            if (!foundBindings.Contains("this") && lex._record.HasThisBinding())
            {
                result.Add("this", lex._record.GetThisBinding());
                foundBindings.Add("this");
            }

            foreach (var binding in bindings)
            {
                if (foundBindings.Contains(binding))
                {
                    // This binding is shadowed by earlier scope
                    continue;
                }
                var jsValue = lex._record.GetBindingValue(binding, false);

                switch (jsValue)
                {
                    case ICallable _:
                        // TODO: Callables aren't added - but maybe they should be.
                        break;
                    case null:
                        // Uninitialized consts and lets in scope are shown as "undefined" in recent Chromium debugger.
                        // TODO: Check if null result from GetBindingValue is only true for uninitialized const/let.
                        break;
                    default:
                        foundBindings.Add(binding);
                        result.Add(binding, jsValue);
                        break;
                }
            }
            return result;
        }
    }
}
