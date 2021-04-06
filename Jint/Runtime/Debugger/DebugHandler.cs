using System;
using Esprima;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Debugger
{
    public class DebugHandler
    {
        public delegate StepMode DebugStepDelegate(object sender, DebugInformation e);
        public delegate StepMode BreakDelegate(object sender, DebugInformation e);

        private enum PauseType
        {
            Step,
            Break
        }

        private readonly Engine _engine;
        private bool _paused;
        private int _steppingDepth;

        public event DebugStepDelegate Step;
        public event BreakDelegate Break;

        internal DebugHandler(Engine engine)
        {
            _engine = engine;
            _steppingDepth = int.MaxValue;
        }

        public BreakPointCollection BreakPoints { get; } = new BreakPointCollection();

        internal void OnStep(Statement statement)
        {
            // Don't reenter if we're already paused (e.g. when evaluating a getter in a Break/Step handler)
            if (_paused)
            {
                return;
            }

            Location location = statement.Location;
            BreakPoint breakpoint = BreakPoints.FindMatch(_engine, location);

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

            BreakPoint breakpoint = BreakPoints.FindMatch(_engine, location);

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
                // Conventionally, sender should be DebugHandler - but Engine is more useful
                PauseType.Step => Step?.Invoke(_engine, info),
                PauseType.Break => Break?.Invoke(_engine, info),
                _ => throw new ArgumentException("Invalid pause type", nameof(type))
            };
            
            _paused = false;
            
            HandleNewStepMode(result);
        }

        internal void OnBreak(Statement statement)
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
                _steppingDepth = newStepMode switch
                {
                    StepMode.Over => _engine.CallStack.Count,// Resume stepping when we're back at this level of the call stack
                    StepMode.Out => _engine.CallStack.Count - 1,// Resume stepping when we've popped the call stack
                    StepMode.None => int.MinValue,// Never step
                    _ => int.MaxValue,// Always step
                };
            }
        }

        private DebugInformation CreateDebugInformation(Statement statement, Location? currentLocation, JsValue returnValue)
        {
            return new DebugInformation(
                statement,
                new DebugCallStack(_engine, currentLocation ?? statement.Location, _engine.CallStack, returnValue),
                _engine.CurrentMemoryUsage
            );
        }
    }
}
