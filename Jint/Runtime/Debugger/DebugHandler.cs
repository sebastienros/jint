using Jint.Native;
using Jint.Runtime.Interpreter;

namespace Jint.Runtime.Debugger;

public enum PauseType
{
    Skip,
    Step,
    Break,
    DebuggerStatement
}

public class DebugHandler
{
    public delegate void BeforeEvaluateEventHandler(object sender, Program ast);
    public delegate StepMode DebugEventHandler(object sender, DebugInformation e);

    private readonly Engine _engine;
    private bool _paused;
    private int _steppingDepth;

    /// <summary>
    /// Triggered before the engine executes/evaluates the parsed AST of a script or module.
    /// </summary>
    public event BeforeEvaluateEventHandler? BeforeEvaluate;

    /// <summary>
    /// The Step event is triggered before the engine executes a step-eligible execution point.
    /// </summary>
    /// <remarks>
    /// If the current step mode is <see cref="StepMode.None"/>, this event is never triggered. The script may
    /// still be paused by a debugger statement or breakpoint, but these will trigger the
    /// <see cref="Break"/> event.
    /// </remarks>
    public event DebugEventHandler? Step;

    /// <summary>
    /// The Break event is triggered when a breakpoint or debugger statement is hit.
    /// </summary>
    /// <remarks>
    /// This is event is not triggered if the current script location was reached by stepping. In that case, only
    /// the <see cref="Step"/> event is triggered.
    /// </remarks>
    public event DebugEventHandler? Break;


    /// <summary>
    /// The Skip event is triggered for each execution point, when the point doesn't trigger a <see cref="Step"/>
    /// or <see cref="Break"/> event.
    /// </summary>
    public event DebugEventHandler? Skip;

    internal DebugHandler(Engine engine, StepMode initialStepMode)
    {
        _engine = engine;
        HandleNewStepMode(initialStepMode);
    }

    private bool IsStepping => _engine.CallStack.Count <= _steppingDepth;

    /// <summary>
    /// The location of the current (step-eligible) AST node being executed.
    /// </summary>
    /// <remarks>
    /// The location is available as long as DebugMode is enabled - i.e. even when not stepping
    /// or hitting a breakpoint.
    /// </remarks>
    public SourceLocation? CurrentLocation { get; private set; }

    /// <summary>
    /// Collection of active breakpoints for the engine.
    /// </summary>
    public BreakPointCollection BreakPoints { get; } = new BreakPointCollection();

    /// <summary>
    /// Evaluates a script (expression) within the current execution context.
    /// </summary>
    /// <remarks>
    /// Internally, this is used for evaluating breakpoint conditions, but may also be used for e.g. watch lists
    /// in a debugger.
    /// </remarks>
    public JsValue Evaluate(in Prepared<Script> preparedScript)
    {
        if (!preparedScript.IsValid)
        {
            ExceptionHelper.ThrowInvalidPreparedScriptArgumentException(nameof(preparedScript));
        }

        var context = _engine._activeEvaluationContext;
        if (context == null)
        {
            throw new DebugEvaluationException("Jint has no active evaluation context");
        }
        var callStackSize = _engine.CallStack.Count;

        var list = new JintStatementList(null, preparedScript.Program.Body);
        Completion result;
        try
        {
            result = list.Execute(context);
        }
        catch (Exception ex)
        {
            // An error in the evaluation may return a Throw Completion, or it may throw an exception:
            throw new DebugEvaluationException("An error occurred during debugger evaluation", ex);
        }
        finally
        {
            // Restore call stack
            while (_engine.CallStack.Count > callStackSize)
            {
                _engine.CallStack.Pop();
            }
        }

        if (result.Type == CompletionType.Throw)
        {
            // TODO: Should we return an error here? (avoid exception overhead, since e.g. breakpoint
            // evaluation may be high volume.
            var error = result.GetValueOrDefault();
            var ex = new JavaScriptException(error).SetJavaScriptCallstack(_engine, result.Location);
            throw new DebugEvaluationException("An error occurred during debugger evaluation", ex);
        }

        return result.GetValueOrDefault();
    }

    /// <inheritdoc cref="Evaluate(in Prepared{Script})" />
    public JsValue Evaluate(string sourceText, ScriptParsingOptions? parsingOptions = null)
    {
        var parserOptions = parsingOptions?.GetParserOptions() ?? _engine.GetActiveParserOptions();
        var parser = _engine.GetParserFor(parserOptions);
        try
        {
            var script = parser.ParseScript(sourceText, "evaluation");
            return Evaluate(new Prepared<Script>(script, parserOptions));
        }
        catch (ParseErrorException ex)
        {
            throw new DebugEvaluationException("An error occurred during debugger expression parsing", ex);
        }
    }

    internal void OnBeforeEvaluate(Program ast)
    {
        if (ast != null)
        {
            BeforeEvaluate?.Invoke(_engine, ast);
        }
    }

    internal void OnStep(Node node)
    {
        // Don't reenter if we're already paused (e.g. when evaluating a getter in a Break/Step handler)
        if (_paused)
        {
            return;
        }
        _paused = true;

        CheckBreakPointAndPause(node, node.Location);
    }

    internal void OnReturnPoint(Node functionBody, JsValue returnValue)
    {
        // Don't reenter if we're already paused (e.g. when evaluating a getter in a Break/Step handler)
        if (_paused)
        {
            return;
        }
        _paused = true;

        var bodyLocation = functionBody.Location;
        var functionBodyEnd = bodyLocation.End;
        var location = SourceLocation.From(functionBodyEnd, functionBodyEnd, bodyLocation.SourceFile);

        CheckBreakPointAndPause(node: null, location, returnValue);
    }

    private void CheckBreakPointAndPause(
        Node? node,
        in SourceLocation location,
        JsValue? returnValue = null)
    {
        CurrentLocation = location;

        // Even if we matched a breakpoint, if we're stepping, the reason we're pausing is the step.
        // Still, we need to include the breakpoint at this location, in case the debugger UI needs to update
        // e.g. a hit count.
        var breakLocation = new BreakLocation(location.SourceFile, location.Start);
        var breakPoint = BreakPoints.FindMatch(this, breakLocation);

        PauseType pauseType;

        if (IsStepping)
        {
            pauseType = PauseType.Step;
        }
        else if (breakPoint != null)
        {
            pauseType = PauseType.Break;
        }
        else if (node?.Type == NodeType.DebuggerStatement &&
                 _engine.Options.Debugger.StatementHandling == DebuggerStatementHandling.Script)
        {
            pauseType = PauseType.DebuggerStatement;
        }
        else
        {
            pauseType = PauseType.Skip;
        }

        Pause(pauseType, node, location, returnValue, breakPoint);

        _paused = false;
    }

    private void Pause(
        PauseType type,
        Node? node,
        in SourceLocation location,
        JsValue? returnValue = null,
        BreakPoint? breakPoint = null)
    {
        var info = new DebugInformation(
            engine: _engine,
            currentNode: node,
            currentLocation: location,
            returnValue: returnValue,
            currentMemoryUsage: _engine.CurrentMemoryUsage,
            pauseType: type,
            breakPoint: breakPoint
        );

        StepMode? result = type switch
        {
            // Conventionally, sender should be DebugHandler - but Engine is more useful
            PauseType.Skip => Skip?.Invoke(_engine, info),
            PauseType.Step => Step?.Invoke(_engine, info),
            PauseType.Break => Break?.Invoke(_engine, info),
            PauseType.DebuggerStatement => Break?.Invoke(_engine, info),
            _ => throw new ArgumentException("Invalid pause type", nameof(type))
        };

        HandleNewStepMode(result);
    }

    private void HandleNewStepMode(StepMode? newStepMode)
    {
        if (newStepMode != null)
        {
            _steppingDepth = newStepMode switch
            {
                StepMode.Over => _engine.CallStack.Count,// Resume stepping when back at this level of the stack
                StepMode.Out => _engine.CallStack.Count - 1,// Resume stepping when we've popped the stack
                StepMode.None => int.MinValue,// Never step
                _ => int.MaxValue,// Always step
            };
        }
    }
}
