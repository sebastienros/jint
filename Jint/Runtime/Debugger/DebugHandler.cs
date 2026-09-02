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
    public delegate void ExceptionThrownEventHandler(object sender, ExceptionThrownEventArgs e);

    private readonly Engine _engine;
    private bool _paused;
    private int _steppingDepth;
    private SourceLocation? _currentLocation;

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

    /// <summary>
    /// Triggered when a JavaScript exception is thrown, whether or not it will be caught by a try/catch block.
    /// This is analogous to a first-chance exception notification.
    /// </summary>
    /// <remarks>
    /// If a catch block re-throws the exception (e.g. <c>throw e;</c>), the event will fire again for the new throw.
    /// </remarks>
    public event ExceptionThrownEventHandler? ExceptionThrown;

    internal DebugHandler(Engine engine, StepMode initialStepMode)
    {
        _engine = engine;
        BreakPoints = new BreakPointCollection();
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
    public SourceLocation? CurrentLocation
    {
        get
        {
            using var ownership = _engine.EnterHostCall();
            return _currentLocation;
        }
        private set => _currentLocation = value;
    }

    /// <summary>
    /// Collection of active breakpoints for the engine.
    /// </summary>
    public BreakPointCollection BreakPoints { get; }

    /// <summary>
    /// Evaluates a script (expression) within the current execution context.
    /// </summary>
    /// <remarks>
    /// Internally, this is used for evaluating breakpoint conditions, but may also be used for e.g. watch lists
    /// in a debugger.
    /// </remarks>
    public JsValue Evaluate(in Prepared<Script> preparedScript)
    {
        using var ownership = _engine.EnterHostCall();
        if (!preparedScript.IsValid)
        {
            Throw.InvalidPreparedScriptArgumentException(nameof(preparedScript));
        }

        if (!_engine.IsEvaluationInProgress)
        {
            throw new DebugEvaluationException("Jint has no active evaluation context");
        }

        var context = _engine._evaluationContext;
        var callStackSize = _engine.CallStack.Count;
        var parsingConstraints = _engine.GetActiveParsingConstraints().Combine(preparedScript.ParsingConstraints);
        ref readonly var activeContext = ref _engine.ExecutionContext;
        var scriptRecord = new ScriptRecord(
            activeContext.Realm,
            preparedScript.Program,
            preparedScript.Program.Location.SourceFile,
            parsingConstraints,
            preparedScript.ParserOptions);
        var debuggerExecutionContext = new Environments.ExecutionContext(
            scriptRecord,
            activeContext.LexicalEnvironment,
            activeContext.VariableEnvironment,
            activeContext.PrivateEnvironment,
            activeContext.Realm,
            parserOptions: preparedScript.ParserOptions,
            strict: activeContext.Strict);

        var list = new JintStatementList(null, preparedScript.Program.Body);
        Completion result;
        var previousDebuggerEvaluating = _engine._debuggerEvaluating;
        var previousParserOptions = _engine._parserOptionsOverride;
        var previousParsingConstraints = _engine._parsingConstraintsOverride;
        _engine._debuggerEvaluating = true;
        _engine._parserOptionsOverride = preparedScript.ParserOptions;
        _engine._parsingConstraintsOverride = parsingConstraints;
        _engine.EnterExecutionContext(in debuggerExecutionContext);
        try
        {
            result = list.Execute(context);
        }
        catch (Exception ex) when (!Throw.MustPropagateHostException(ex))
        {
            // An error in the evaluation may return a Throw Completion, or it may throw an exception:
            throw new DebugEvaluationException("An error occurred during debugger evaluation", ex);
        }
        finally
        {
            _engine.LeaveExecutionContext();
            _engine._debuggerEvaluating = previousDebuggerEvaluating;
            _engine._parserOptionsOverride = previousParserOptions;
            _engine._parsingConstraintsOverride = previousParsingConstraints;

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
        using var ownership = _engine.EnterHostCall();
        var parser = parsingOptions is null
            ? _engine.GetParserFor(_engine.GetActiveParserOptions())
            : _engine.GetParserFor(parsingOptions);
        try
        {
            var script = parser.ParseScript(sourceText, "evaluation");
            var prepared = new Prepared<Script>(
                script,
                parser.Options,
                parsingConstraints: parser.Constraints);
            var previousParserOptions = _engine._parserOptionsOverride;
            var previousParsingConstraints = _engine._parsingConstraintsOverride;
            _engine._parserOptionsOverride = parser.Options;
            _engine._parsingConstraintsOverride = parser.Constraints;
            try
            {
                return Evaluate(in prepared);
            }
            finally
            {
                _engine._parserOptionsOverride = previousParserOptions;
                _engine._parsingConstraintsOverride = previousParsingConstraints;
            }
        }
        catch (ParseErrorException ex)
        {
            throw new DebugEvaluationException("An error occurred during debugger expression parsing", ex);
        }
    }

    /// <summary>
    /// Returns every position in <paramref name="program"/> that the debugger stops at, ordered by line,
    /// then column, then kind.
    /// </summary>
    /// <param name="program">The parsed script or module to walk.</param>
    /// <remarks>
    /// <para>
    /// This is the set <see cref="Step"/> and <see cref="Break"/> report when the program runs with every
    /// branch taken: every statement except a block, the test and update expressions of a loop header, the
    /// binding target of <c>for</c>-<c>in</c>/<c>of</c>, an arrow function's expression body, and the
    /// implicit return point at the end of every function body. A breakpoint matches a node's exact start
    /// position, so this is what a debugger snaps a line-and-column request onto.
    /// </para>
    /// <para>
    /// Static because the walk is purely syntactic: it reads nothing from an engine, so a host can ask for
    /// the locations of a <see cref="Prepared{T}"/> program before any engine runs it. The result is
    /// recomputed per call; cache it against the <see cref="Program"/> when answering many queries.
    /// </para>
    /// <para>
    /// No location has kind <c>call</c>, which the Chrome DevTools Protocol also defines: Jint's step lane
    /// pauses on statements, never on the calls inside one, so reporting a call position would name a place
    /// the debugger never stops. Code reached through <c>eval</c> is a program of its own and is not walked.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<StepLocation> GetStepLocations(Program program)
    {
        if (program is null)
        {
            Throw.ArgumentNullException(nameof(program));
        }

        return StepLocationCollector.Collect(program);
    }

    /// <summary>
    /// Returns the step locations of <paramref name="program"/> that start at or after one position and
    /// before another.
    /// </summary>
    /// <param name="program">The parsed script or module to walk.</param>
    /// <param name="startLine">The 1-based line the range starts on.</param>
    /// <param name="startColumn">The 0-based column the range starts at.</param>
    /// <param name="endLine">The 1-based line the range ends before.</param>
    /// <param name="endColumn">The 0-based column the range ends before.</param>
    /// <remarks>
    /// <para>
    /// The start is inclusive and the end exclusive, which is what the Chrome DevTools Protocol's
    /// <c>Debugger.getPossibleBreakpoints</c> expects. Lines are 1-based here and 0-based there.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<StepLocation> GetStepLocations(Program program, int startLine, int startColumn, int endLine, int endColumn)
    {
        var all = GetStepLocations(program);
        var result = new List<StepLocation>();
        for (var i = 0; i < all.Count; i++)
        {
            var location = all[i];
            if (IsBefore(location, startLine, startColumn) || !IsBefore(location, endLine, endColumn))
            {
                continue;
            }

            result.Add(location);
        }

        return result;
    }

    /// <summary>
    /// Returns the first step location of <paramref name="program"/> at or after the given position, or
    /// <see langword="null"/> when none follows it.
    /// </summary>
    /// <param name="program">The parsed script or module to walk.</param>
    /// <param name="line">The 1-based line asked for.</param>
    /// <param name="column">The 0-based column asked for.</param>
    /// <remarks>
    /// <para>
    /// This is the snap a debugger front end needs: it asks for a breakpoint at the start of a line, and an
    /// indented statement — or one on a later line, when the line asked for holds no code — is where the
    /// engine will actually stop.
    /// </para>
    /// </remarks>
    public static StepLocation? FindStepLocation(Program program, int line, int column)
    {
        var all = GetStepLocations(program);
        for (var i = 0; i < all.Count; i++)
        {
            if (!IsBefore(all[i], line, column))
            {
                return all[i];
            }
        }

        return null;
    }

    private static bool IsBefore(StepLocation location, int line, int column)
    {
        return location.Line < line || (location.Line == line && location.Column < column);
    }

    internal void OnExceptionThrown(JsValue thrownValue, in SourceLocation location)
    {
        if (ExceptionThrown is null)
        {
            return;
        }

        // Don't reenter if we're already paused (e.g. when evaluating in a Step/Break handler)
        if (_paused)
        {
            return;
        }

        var args = new ExceptionThrownEventArgs(_engine, thrownValue, in location);
        ExceptionThrown.Invoke(_engine, args);
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

        CheckBreakPointAndPause(node: null, in location, returnValue);
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

        Pause(pauseType, node, in location, returnValue, breakPoint);

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
            currentLocation: in location,
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
