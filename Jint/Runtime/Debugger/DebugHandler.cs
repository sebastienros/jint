using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Debugger;

public enum PauseType
{
    Skip,
    Step,
    Break,
    DebuggerStatement,

    /// <summary>
    /// A JavaScript exception was thrown and <see cref="DebugHandler.PauseOnExceptions"/> asked for it.
    /// </summary>
    Exception
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
    /// Stamped on every <see cref="DebugCallStack"/> this handler hands out and bumped when the
    /// notification that produced it returns, so that <see cref="Evaluate(string, CallFrame, ScriptParsingOptions)"/>
    /// can tell a frame of the current pause from one a host kept past it.
    /// </summary>
    private long _generation;

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
    /// Gets or sets which thrown exceptions stop the engine. Defaults to <see cref="ExceptionPauseMode.None"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The engine stops at the throw site, before anything unwinds, so the call stack still holds the frame
    /// that threw and its scopes and <see cref="Evaluate(string, CallFrame, ScriptParsingOptions)"/> answer
    /// for it. The pause raises <see cref="Break"/> with <see cref="PauseType.Exception"/>, and
    /// <see cref="DebugInformation.ThrownValue"/> carries the value that was thrown.
    /// </para>
    /// <para>
    /// <see cref="ExceptionPauseMode.Uncaught"/> means no <c>catch</c> clause is executing on the stack — a
    /// <c>finally</c>-only <c>try</c> does not count, a <c>catch</c> in a calling function does. Two
    /// boundaries end that search, because a throw crossing either becomes something other than an exception:
    /// a host entry, whose caller receives a <see cref="JavaScriptException"/>, and an async function body,
    /// whose throw becomes a rejection of its own promise. So an async function that throws is uncaught even
    /// when its caller wrapped the call in <c>try</c>/<c>catch</c>, which is what a user asking to stop on
    /// uncaught exceptions wants and what a rejection nobody handles turns out to be.
    /// </para>
    /// <para>
    /// A rejection with no throw behind it — <c>Promise.reject(x)</c> — never stops the engine. Nothing here
    /// replaces <see cref="ExceptionThrown"/>, which still reports every throw whatever this is set to.
    /// </para>
    /// </remarks>
    public ExceptionPauseMode PauseOnExceptions { get; set; }

    /// <summary>
    /// Evaluates a script (expression) within the current execution context.
    /// </summary>
    /// <remarks>
    /// Internally, this is used for evaluating breakpoint conditions, but may also be used for e.g. watch lists
    /// in a debugger.
    /// </remarks>
    public JsValue Evaluate(in Prepared<Script> preparedScript) => EvaluateCore(in preparedScript, frame: null);

    /// <summary>
    /// Evaluates a script (expression) in the environment of <paramref name="frame"/> rather than in the
    /// innermost one.
    /// </summary>
    /// <param name="preparedScript">The prepared expression or statement list to run.</param>
    /// <param name="frame">A frame of the call stack the debugger is currently stopped in.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="frame"/> belongs to another engine, or to a pause this engine has already left.
    /// </exception>
    /// <remarks>
    /// <para>
    /// The frame's scope chain is what the expression resolves against, so a binding the innermost frame
    /// shadows is read — and written — as that frame sees it, and <c>this</c> and <c>arguments</c> are the
    /// frame's own. Passing <see cref="DebugInformation.CurrentCallFrame"/> is exactly
    /// <see cref="Evaluate(in Prepared{Script})"/>.
    /// </para>
    /// <para>
    /// Frames belong to the pause they were taken in. One kept past the <see cref="Break"/>,
    /// <see cref="Step"/> or <see cref="ExceptionThrown"/> handler that produced it names environments the
    /// engine has since left, so it is refused rather than evaluated against.
    /// </para>
    /// </remarks>
    public JsValue Evaluate(in Prepared<Script> preparedScript, CallFrame frame)
    {
        if (frame is null)
        {
            Throw.ArgumentNullException(nameof(frame));
        }

        return EvaluateCore(in preparedScript, frame);
    }

    private JsValue EvaluateCore(in Prepared<Script> preparedScript, CallFrame? frame)
    {
        using var ownership = _engine.EnterHostCall();
        if (!preparedScript.IsValid)
        {
            Throw.InvalidPreparedScriptArgumentException(nameof(preparedScript));
        }

        // Before the evaluation-context test, so that a frame the engine has moved past is always reported
        // as the misuse it is, rather than as "no active evaluation context" once the run is over.
        if (frame is not null)
        {
            VerifyFrameBelongsToCurrentPause(frame);
        }

        if (!_engine.IsEvaluationInProgress)
        {
            throw new DebugEvaluationException("Jint has no active evaluation context");
        }

        var context = _engine._evaluationContext;
        var callStackSize = _engine.CallStack.Count;
        var parsingConstraints = _engine.GetActiveParsingConstraints().Combine(preparedScript.ParsingConstraints);
        ref readonly var activeContext = ref _engine.ExecutionContext;

        // The top frame IS the active execution context (DebugCallStack builds it from exactly that), so
        // taking the active one keeps the frame overload observably identical to the frameless one there.
        var target = frame is null || frame.Index == 0
            ? new FrameEnvironments(
                activeContext.LexicalEnvironment,
                activeContext.VariableEnvironment,
                activeContext.PrivateEnvironment,
                activeContext.Realm,
                activeContext.Strict)
            : DescribeFrame(frame, in activeContext);

        var scriptRecord = new ScriptRecord(
            target.Realm,
            preparedScript.Program,
            preparedScript.Program.Location.SourceFile,
            parsingConstraints,
            preparedScript.ParserOptions);
        var debuggerExecutionContext = new Environments.ExecutionContext(
            scriptRecord,
            target.LexicalEnvironment,
            target.VariableEnvironment,
            target.PrivateEnvironment,
            target.Realm,
            parserOptions: preparedScript.ParserOptions,
            strict: target.Strict);

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
        => EvaluateCore(sourceText, frame: null, parsingOptions);

    /// <inheritdoc cref="Evaluate(in Prepared{Script}, CallFrame)" />
    /// <param name="sourceText">The expression or statement list to parse and run.</param>
    /// <param name="frame">A frame of the call stack the debugger is currently stopped in.</param>
    /// <param name="parsingOptions">Parsing options, or <see langword="null"/> for the engine's own.</param>
    public JsValue Evaluate(string sourceText, CallFrame frame, ScriptParsingOptions? parsingOptions = null)
    {
        if (frame is null)
        {
            Throw.ArgumentNullException(nameof(frame));
        }

        return EvaluateCore(sourceText, frame, parsingOptions);
    }

    private JsValue EvaluateCore(string sourceText, CallFrame? frame, ScriptParsingOptions? parsingOptions)
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
                return EvaluateCore(in prepared, frame);
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
    /// The parts of an <see cref="Environments.ExecutionContext"/> a debugger evaluation has to establish
    /// to run as if it were code of one particular call frame.
    /// </summary>
    private readonly record struct FrameEnvironments(
        Environment LexicalEnvironment,
        Environment VariableEnvironment,
        PrivateEnvironment? PrivateEnvironment,
        Realm Realm,
        bool Strict);

    /// <summary>
    /// Reconstructs a non-top frame's execution context. Only the lexical environment is recorded per
    /// frame; the rest is derived from it and from the frame's function, which is where the caller's
    /// context took them from in the first place.
    /// </summary>
    private static FrameEnvironments DescribeFrame(CallFrame frame, in Environments.ExecutionContext activeContext)
    {
        var lexicalEnvironment = frame.LexicalEnvironment;
        var variableEnvironment = FindVariableEnvironment(lexicalEnvironment);
        var function = frame.Function;

        // A function's [[PrivateEnvironment]] and [[Realm]] are exactly what PrepareForOrdinaryCall puts
        // on its execution context. The global frame has no function, so it keeps the running realm, and
        // its code is strict only when it is a module's.
        return new FrameEnvironments(
            lexicalEnvironment,
            variableEnvironment,
            function?._privateEnvironment,
            function?._realm ?? activeContext.Realm,
            function?._functionDefinition?.Strict ?? variableEnvironment is ModuleEnvironment);
    }

    /// <summary>
    /// Walks out of block, <c>catch</c> and <c>with</c> scopes to the environment a <c>var</c> declaration
    /// of the frame would land in.
    /// </summary>
    private static Environment FindVariableEnvironment(Environment lexicalEnvironment)
    {
        var environment = lexicalEnvironment;
        while (environment is not null)
        {
            if (environment is FunctionEnvironment or GlobalEnvironment or ModuleEnvironment)
            {
                return environment;
            }

            environment = environment._outerEnv;
        }

        return lexicalEnvironment;
    }

    private void VerifyFrameBelongsToCurrentPause(CallFrame frame)
    {
        if (!ReferenceEquals(frame.Engine, _engine))
        {
            Throw.InvalidOperationException("The call frame belongs to a different engine");
        }

        if (frame.Generation != _generation)
        {
            Throw.InvalidOperationException(
                "The call frame belongs to an execution point this engine has already left, and the environments it names are no longer on the stack");
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

    /// <summary>
    /// Reports a thrown JavaScript exception to <see cref="ExceptionThrown"/>, and stops the engine when
    /// <see cref="PauseOnExceptions"/> asks for it.
    /// </summary>
    /// <param name="thrownValue">The value that was thrown.</param>
    /// <param name="location">Where in the source it was thrown.</param>
    /// <param name="reRaisedAtBodyBoundary">
    /// Whether this is the same throw leaving a function, generator or <c>eval</c> body rather than a new
    /// one. The event fires either way — it always has — but the pause happens only for the throw itself.
    /// </param>
    internal void OnExceptionThrown(JsValue thrownValue, in SourceLocation location, bool reRaisedAtBodyBoundary = false)
    {
        var pauseMode = reRaisedAtBodyBoundary ? ExceptionPauseMode.None : PauseOnExceptions;
        if (ExceptionThrown is null && pauseMode == ExceptionPauseMode.None)
        {
            return;
        }

        // Don't reenter if we're already paused (e.g. when evaluating in a Step/Break handler)
        if (_paused)
        {
            return;
        }

        if (ExceptionThrown is not null)
        {
            var args = new ExceptionThrownEventArgs(_engine, thrownValue, in location, _generation);
            ExceptionThrown.Invoke(_engine, args);
            _generation++;
        }

        if (pauseMode == ExceptionPauseMode.None)
        {
            return;
        }

        // Nothing on the CLR stack between the throw and here has popped a call frame, so the engine's
        // count of executing try-with-catch blocks is still the one the throw was raised under.
        var uncaught = _engine._tryCatchDepth == 0;
        if (pauseMode == ExceptionPauseMode.Uncaught && !uncaught)
        {
            return;
        }

        _paused = true;

        Pause(PauseType.Exception, node: null, in location, thrownValue: thrownValue, isUncaught: uncaught);

        _generation++;
        _paused = false;
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

        // Whatever the handler kept — a DebugInformation, a CallFrame — names environments that are about
        // to be left, so a frame handed back later can be told apart from one of the pause still running.
        _generation++;
        _paused = false;
    }

    private void Pause(
        PauseType type,
        Node? node,
        in SourceLocation location,
        JsValue? returnValue = null,
        BreakPoint? breakPoint = null,
        JsValue? thrownValue = null,
        bool isUncaught = false)
    {
        var info = new DebugInformation(
            engine: _engine,
            currentNode: node,
            currentLocation: in location,
            returnValue: returnValue,
            currentMemoryUsage: _engine.CurrentMemoryUsage,
            pauseType: type,
            breakPoint: breakPoint,
            generation: _generation,
            thrownValue: thrownValue,
            isUncaught: isUncaught
        );

        StepMode? result = type switch
        {
            // Conventionally, sender should be DebugHandler - but Engine is more useful
            PauseType.Skip => Skip?.Invoke(_engine, info),
            PauseType.Step => Step?.Invoke(_engine, info),
            PauseType.Break => Break?.Invoke(_engine, info),
            PauseType.DebuggerStatement => Break?.Invoke(_engine, info),
            PauseType.Exception => Break?.Invoke(_engine, info),
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
