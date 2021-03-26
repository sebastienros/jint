using System;
using System.Collections.Generic;
using System.Linq;
using Esprima;
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

            Location location = statement.Location;
            BreakPoint breakpoint = _engine.BreakPoints.FirstOrDefault(breakPoint => BpTest(location, breakPoint));

            if (breakpoint != null)
            {
                Pause(PauseType.Break, statement);
            }
            else if (_engine.CallStack.Count <= _steppingDepth)
            {
                Pause(PauseType.Step, statement);
            }
        }

        internal void OnReturnPoint(Node functionBody, JsValue returnValue)
        {
            // Don't reenter if we're already paused (e.g. when evaluating a getter in a Break/Step handler)
            if (_paused)
            {
                return;
            }

            var bodyLocation = functionBody.Location;
            var functionBodyEnd = bodyLocation.End;
            var location = new Location(functionBodyEnd, functionBodyEnd, bodyLocation.Source);

            BreakPoint breakpoint = _engine.BreakPoints.FirstOrDefault(breakPoint => BpTest(location, breakPoint));

            if (breakpoint != null)
            {
                Pause(PauseType.Break, statement: null, location, returnValue);
            }
            else if (_engine.CallStack.Count <= _steppingDepth)
            {
                Pause(PauseType.Step, statement: null, location, returnValue);
            }
        }

        private void Pause(PauseType type, Statement statement = null, Location? location = null, JsValue returnValue = null)
        {
            _paused = true;
            
            DebugInformation info = CreateDebugInformation(statement, location ?? statement.Location, returnValue);
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

            Pause(PauseType.Break, statement);
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

        private bool BpTest(Location location, BreakPoint breakpoint)
        {
            if (breakpoint.Source != null)
            {
                if (breakpoint.Source != location.Source)
                {
                    return false;
                }
            }

            bool afterStart, beforeEnd;

            afterStart = (breakpoint.Line == location.Start.Line &&
                             breakpoint.Char >= location.Start.Column);

            if (!afterStart)
            {
                return false;
            }

            beforeEnd = breakpoint.Line < location.End.Line
                        || (breakpoint.Line == location.End.Line &&
                            breakpoint.Char <= location.End.Column);

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

        private DebugInformation CreateDebugInformation(Statement statement, Location? currentLocation, JsValue returnValue)
        {
            return new DebugInformation
            {
                CurrentStatement = statement,
                Location = currentLocation ?? statement.Location,
                CallStack = new DebugCallStack(_engine, currentLocation ?? statement.Location, _engine.CallStack, returnValue),
                CurrentMemoryUsage = _engine.CurrentMemoryUsage
            };
        }
    }
}
