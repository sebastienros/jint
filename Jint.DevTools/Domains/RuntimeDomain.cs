using System.Globalization;
using System.Text.Json;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Runtime;
using Jint.DevTools.Session;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Debugger;
using Jint.Runtime.Interop;

namespace Jint.DevTools.Domains;

/// <summary>
/// The <c>Runtime</c> domain: the execution context, evaluation, the handles a client holds values by, and
/// the bindings script calls back through.
/// </summary>
/// <remarks>
/// <para>
/// Every member runs on the engine thread, brought there by <see cref="EngineDispatcher"/>. The one thing it
/// must never do is drain the event loop — that is what the <c>awaitPromise</c> paths attach reactions for
/// rather than waiting.
/// </para>
/// <para>
/// <b>One mapper per attachment.</b> A handle is a promise to keep a value alive until the client releases
/// it, so every identifier this domain mints is billed to this attachment and released when it detaches.
/// The table itself belongs to the target, because the values in it belong to the engine.
/// </para>
/// <para>
/// See <see href="https://chromedevtools.github.io/devtools-protocol/tot/Runtime/"/>.
/// </para>
/// </remarks>
internal sealed partial class RuntimeDomain : RuntimeDomainBase, IBindingListener, ITargetObserver
{
    /// <summary>
    /// The one execution context an engine target has. Chrome numbers page contexts from 1 and so does this;
    /// there is no second realm to number, because a <c>ShadowRealm</c> is not something a client can address.
    /// </summary>
    private const int MainExecutionContextId = 1;

    private readonly EngineTarget _target;
    private readonly RemoteObjectMapper _objects;

    private int _exceptionId;

    internal RuntimeDomain(EngineTarget target)
    {
        _target = target;
        _objects = new RemoteObjectMapper(target, this);
    }

    /// <summary>
    /// Releases everything this attachment holds of the engine: its handles, and its share of every binding.
    /// </summary>
    /// <remarks>
    /// Called from a transport thread, when the client detaches or the connection goes. Neither half touches
    /// the engine — dropping a reference and dropping a subscription both run no script — which is what
    /// makes detaching answerable without a round trip through the mailbox.
    /// </remarks>
    internal void Detach()
    {
        _objects.ReleaseAll();
        _target.Bindings.RemoveAll(this);
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> EnableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkEnabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <inheritdoc/>
    protected override async ValueTask<EmptyResult> DisableAsync(EmptyParameters parameters, CommandContext context)
    {
        await MarkDisabledAsync(context).ConfigureAwait(false);
        return EmptyResult.Instance;
    }

    /// <summary>
    /// Announces the one execution context, which is what a client waits for before it evaluates anything.
    /// </summary>
    /// <remarks>
    /// Replayed on every <c>enable</c> rather than raised once when the context appeared: the context
    /// outlives every session, so a client attaching later would otherwise never hear of it. That is V8's
    /// behaviour too.
    /// </remarks>
    protected override async ValueTask OnEnabledAsync(CommandContext context)
    {
        var created = new ExecutionContextCreatedEvent
        {
            Context = new ExecutionContextDescription
            {
                Id = MainExecutionContextId,

                // An engine target has no document, so it has no origin and no name to give. Chrome sends
                // the empty string for both on a Node target and clients read them as opaque.
                Origin = "",
                Name = "",
                UniqueId = _target.TargetId + "." + MainExecutionContextId.ToString(CultureInfo.InvariantCulture),
                AuxData = AuxData,
            },
        };

        await EmitAsync(RuntimeEvents.ExecutionContextCreated(created), context.CancellationToken).ConfigureAwait(false);
        await ReplayConsoleAsync(context).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> RunIfWaitingForDebuggerAsync(EmptyParameters parameters, CommandContext context)
    {
        _target.Dispatcher.ReleaseDebuggerWait();
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Empties the journal every attachment replays, and releases the handles minted for what was in it.
    /// </summary>
    /// <remarks>
    /// The journal is the target's, so this discards for every attachment rather than only for the one that
    /// asked. That is what the command means — Chrome's console history is the page's and not the session's
    /// — and it is why a client sends it when a user clears the console.
    /// </remarks>
    protected override ValueTask<EmptyResult> DiscardConsoleEntriesAsync(EmptyParameters parameters, CommandContext context)
    {
        _target.Console.Clear(_target.RemoteObjects);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Answers a stable identifier for the engine, which is what a client groups targets sharing a heap by.
    /// </summary>
    /// <remarks>
    /// One engine is one isolate here: engines share nothing, and a <c>JsValue</c> crossing between two of
    /// them is unsupported, so the target's own identifier is exactly as unique as the isolate is.
    /// </remarks>
    protected override ValueTask<GetIsolateIdResponse> GetIsolateIdAsync(EmptyParameters parameters, CommandContext context)
    {
        return new ValueTask<GetIsolateIdResponse>(new GetIsolateIdResponse { Id = _target.TargetId });
    }

    /// <summary>
    /// Answers the process's managed heap, twice, and says so.
    /// </summary>
    /// <remarks>
    /// The protocol asks for a JavaScript heap's used and allocated sizes, and there is no such heap: a
    /// <c>JsValue</c> is a CLR object on the CLR's own garbage-collected heap, shared with the host. What is
    /// answered is <see cref="GC.GetTotalMemory(bool)"/> without forcing a collection, for both figures, and
    /// zero for the two that describe storage a .NET engine does not have separately. A client watching the
    /// number climb across one target is reading something real; a client comparing two targets in one
    /// process is reading the same number twice. <c>engine.Diagnostics.GetMemoryReport()</c> is where a host
    /// gets figures about one engine, and <c>LimitMemory</c> is where it bounds one.
    /// </remarks>
    protected override ValueTask<GetHeapUsageResponse> GetHeapUsageAsync(EmptyParameters parameters, CommandContext context)
    {
        var used = (double) GC.GetTotalMemory(forceFullCollection: false);

        return new ValueTask<GetHeapUsageResponse>(new GetHeapUsageResponse
        {
            UsedSize = used,
            TotalSize = used,
            EmbedderHeapUsedSize = 0,
            BackingStorageSize = 0,
        });
    }

    /// <inheritdoc/>
    protected override ValueTask<EvaluateResponse> EvaluateAsync(EvaluateRequest parameters, CommandContext context)
    {
        RequireMainContext(parameters.ContextId);
        RefuseSideEffectFreeEvaluation(parameters.ThrowOnSideEffect);

        var request = RemoteObjectRequest.From(parameters.ReturnByValue, parameters.GeneratePreview, parameters.ObjectGroup);

        JsValue value;
        try
        {
            value = Evaluate(parameters.Expression);
        }
        catch (JavaScriptException exception)
        {
            // A compile failure arrives here too: the engine raises a SyntaxError for one, with the
            // location filled in, so there is one path rather than two and neither is a protocol error --
            // the command was well formed and it is the client's expression that was not.
            return new ValueTask<EvaluateResponse>(EvaluateFailure(exception, request));
        }

        if (parameters.AwaitPromise == true && value.IsPromise())
        {
            return Settled(value, request, static (result, details) => new EvaluateResponse { Result = result, ExceptionDetails = details });
        }

        return new ValueTask<EvaluateResponse>(new EvaluateResponse { Result = _objects.Describe(value, request) });
    }

    /// <inheritdoc/>
    protected override ValueTask<CallFunctionOnResponse> CallFunctionOnAsync(CallFunctionOnRequest parameters, CommandContext context)
    {
        RequireMainContext(parameters.ExecutionContextId);
        RefuseSideEffectFreeEvaluation(parameters.ThrowOnSideEffect);

        var engine = _target.Engine;

        // The group a client asked for, or the one the receiver already belongs to. Inheriting it is what
        // makes releaseObjectGroup free the handles a client walked to rather than only the one it started
        // from, and it is what V8 does.
        var thisValue = JsValue.Undefined;
        var group = parameters.ObjectGroup;

        if (parameters.ObjectId is { } objectId)
        {
            thisValue = _target.RemoteObjects.Resolve(objectId, out var owningGroup);
            group ??= owningGroup;
        }

        var request = RemoteObjectRequest.From(parameters.ReturnByValue, parameters.GeneratePreview, group);

        JsValue function;
        try
        {
            function = Evaluate(_target.CompiledScripts.Declaration(parameters.FunctionDeclaration));
        }
        catch (ScriptPreparationException exception)
        {
            return Throw.ServerError<ValueTask<CallFunctionOnResponse>>("Given expression does not evaluate to a function", exception.Message);
        }
        catch (JavaScriptException exception)
        {
            return new ValueTask<CallFunctionOnResponse>(CallFailure(exception, request));
        }

        if (!function.IsCallable())
        {
            // Chrome's wording, and the one a client feature-detects a malformed declaration by.
            Throw.ServerError("Given expression does not evaluate to a function");
        }

        var arguments = CallArguments.Resolve(_target, parameters.Arguments);

        JsValue result;
        try
        {
            result = engine.Call(function, thisValue, arguments);
        }
        catch (JavaScriptException exception)
        {
            return new ValueTask<CallFunctionOnResponse>(CallFailure(exception, request));
        }

        if (parameters.AwaitPromise == true && result.IsPromise())
        {
            return Settled(result, request, static (value, details) => new CallFunctionOnResponse { Result = value, ExceptionDetails = details });
        }

        return new ValueTask<CallFunctionOnResponse>(new CallFunctionOnResponse { Result = _objects.Describe(result, request) });
    }

    /// <inheritdoc/>
    protected override ValueTask<AwaitPromiseResponse> AwaitPromiseAsync(AwaitPromiseRequest parameters, CommandContext context)
    {
        var promise = _target.RemoteObjects.Resolve(parameters.PromiseObjectId, out var group);
        if (!promise.IsPromise())
        {
            // Chrome's wording. A client sends this only for a handle it was told is a promise, so getting
            // here means the two disagree about what the handle is.
            Throw.ServerError("Could not find promise with given id");
        }

        var request = RemoteObjectRequest.From(parameters.ReturnByValue, parameters.GeneratePreview, group);
        return Settled(promise, request, static (value, details) => new AwaitPromiseResponse { Result = value, ExceptionDetails = details });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The front end sends this once per error object it is asked to render — an uncaught exception, an
    /// <c>Error</c> a script logged — to get the frames it draws an expandable stack from. Nothing here
    /// remembers an exception past the command that reported it, and nothing needs to: the details are
    /// reconstructed from the object the handle names, which is also what makes the command work for an
    /// error this server never reported at all.
    /// </remarks>
    protected override ValueTask<GetExceptionDetailsResponse> GetExceptionDetailsAsync(
        GetExceptionDetailsRequest parameters,
        CommandContext context)
    {
        var error = _target.RemoteObjects.Resolve(parameters.ErrorObjectId, out _);
        if (!RemoteObjectMapper.IsError(error))
        {
            // Chrome's wording, verbatim: a client feature-detects nothing on it, but a host debugging a
            // handle mix-up reads it.
            Throw.ServerError("errorObjectId is not a JS error object");
        }

        var details = _objects.ErrorDetails(error, NextExceptionId(), MainExecutionContextId, _target.Scripts);
        return new ValueTask<GetExceptionDetailsResponse>(new GetExceptionDetailsResponse { ExceptionDetails = details });
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> ReleaseObjectAsync(ReleaseObjectRequest parameters, CommandContext context)
    {
        // A handle nothing knows about is not an error: a client releasing what it already released, or what
        // a detach took with it, is tidying up, and Chrome answers that with a success too.
        _target.RemoteObjects.Release(parameters.ObjectId);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> ReleaseObjectGroupAsync(ReleaseObjectGroupRequest parameters, CommandContext context)
    {
        _objects.ReleaseGroup(parameters.ObjectGroup);
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<CompileScriptResponse> CompileScriptAsync(CompileScriptRequest parameters, CommandContext context)
    {
        RequireMainContext(parameters.ExecutionContextId);

        Prepared<Acornima.Ast.Script> prepared;
        try
        {
            prepared = Engine.PrepareScript(parameters.Expression, string.IsNullOrEmpty(parameters.SourceURL) ? null : parameters.SourceURL);
        }
        catch (ScriptPreparationException exception)
        {
            // A parse failure is the command's answer rather than a protocol error: the command was well
            // formed and it is the client's source that was not. There is no error *object* to describe,
            // because nothing ran and no realm minted one; the text is what a front end prints.
            return new ValueTask<CompileScriptResponse>(new CompileScriptResponse
            {
                ExceptionDetails = new ExceptionDetails
                {
                    ExceptionId = NextExceptionId(),
                    Text = exception.Message,
                    LineNumber = 0,
                    ColumnNumber = 0,

                    // Through the same mapping every other command publishes a source name under, so a
                    // client that sent a filesystem path is answered the URL a later scriptParsed would
                    // announce rather than a second spelling of one script.
                    Url = string.IsNullOrEmpty(parameters.SourceURL) ? null : ScriptUrl.From(parameters.SourceURL),
                    ExecutionContextId = MainExecutionContextId,
                },
            });
        }

        return new ValueTask<CompileScriptResponse>(new CompileScriptResponse
        {
            // Not persisting is a compile-only syntax check, which is what a client sends it for; Chrome
            // answers that with no identifier rather than with one that addresses nothing.
            ScriptId = parameters.PersistScript ? _target.CompiledScripts.Persist(prepared) : null,
        });
    }

    /// <inheritdoc/>
    protected override ValueTask<RunScriptResponse> RunScriptAsync(RunScriptRequest parameters, CommandContext context)
    {
        RequireMainContext(parameters.ExecutionContextId);

        if (!_target.CompiledScripts.TryGetPersisted(parameters.ScriptId, out var prepared))
        {
            // Chrome's wording for a script identifier that names nothing.
            Throw.ServerError("No script with given id");
        }

        var request = RemoteObjectRequest.From(parameters.ReturnByValue, parameters.GeneratePreview, parameters.ObjectGroup);

        JsValue value;
        try
        {
            value = _target.Engine.Evaluate(prepared);
        }
        catch (JavaScriptException exception)
        {
            return new ValueTask<RunScriptResponse>(new RunScriptResponse
            {
                Result = _objects.Describe(exception.Error, Thrown(request, exception.Error)),
                ExceptionDetails = _objects.Exception(exception, NextExceptionId(), MainExecutionContextId),
            });
        }

        if (parameters.AwaitPromise == true && value.IsPromise())
        {
            return Settled(value, request, static (result, details) => new RunScriptResponse { Result = result, ExceptionDetails = details });
        }

        return new ValueTask<RunScriptResponse>(new RunScriptResponse { Result = _objects.Describe(value, request) });
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> AddBindingAsync(AddBindingRequest parameters, CommandContext context)
    {
        RequireMainContext(parameters.ExecutionContextId);

        // executionContextName is deliberately not checked against anything. It names an isolated world, and
        // an engine target has one context and no worlds; a client that asked for a named one gets the one
        // there is, because a binding it cannot call would fail silently in the page rather than loudly here.
        _target.Bindings.Add(_target.Engine, parameters.Name, this);

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> RemoveBindingAsync(RemoveBindingRequest parameters, CommandContext context)
    {
        _target.Bindings.Remove(_target.Engine, parameters.Name, this);

        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Answers the success a client expects, and formats nothing differently.
    /// </summary>
    /// <remarks>
    /// A custom formatter is a page's own <c>devtoolsFormatters</c> global, which an engine target has no
    /// document to carry. Refusing would tell the DevTools front end that the target is broken; the honest
    /// answer is that the switch is on and nothing on this target reads it.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetCustomObjectFormatterEnabledAsync(SetCustomObjectFormatterEnabledRequest parameters, CommandContext context)
    {
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Answers the success a client expects, and captures no differently.
    /// </summary>
    /// <remarks>
    /// The stacks this package reports come from the engine's own capture, whose depth is the engine's
    /// setting rather than a per-session one.
    /// </remarks>
    protected override ValueTask<EmptyResult> SetMaxCallStackSizeToCaptureAsync(SetMaxCallStackSizeToCaptureRequest parameters, CommandContext context)
    {
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <inheritdoc/>
    void IBindingListener.BindingCalled(string name, string payload)
    {
        EmitDetached(RuntimeEvents.BindingCalled(new BindingCalledEvent
        {
            Name = name,
            Payload = payload,
            ExecutionContextId = MainExecutionContextId,
        }));
    }

    /// <summary>
    /// Evaluates an expression where the engine currently is: at the top of the paused call stack, or at the
    /// top level when nothing is paused.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A paused engine may not be given a new script to run.</b> <c>Engine.Evaluate</c> is
    /// a public entry, and the engine is suspended part-way through a statement of something else;
    /// <c>DebugHandler.Evaluate</c> is the path built for exactly this, running the expression in the active
    /// execution context — which is what makes a console expression see the locals a client is looking at.
    /// </para>
    /// <para>
    /// The debugger wraps whatever the expression threw in a <c>DebugEvaluationException</c>, and a client
    /// asked about the JavaScript rather than about the wrapper, so the inner failure is what comes back.
    /// </para>
    /// </remarks>
    private JsValue Evaluate(string expression)
    {
        if (!_target.IsPaused)
        {
            return _target.Engine.Evaluate(expression);
        }

        try
        {
            return _target.Engine.Debugger.Evaluate(expression);
        }
        catch (DebugEvaluationException exception)
        {
            throw Unwrap(exception);
        }
    }

    /// <inheritdoc cref="Evaluate(System.String)"/>
    private JsValue Evaluate(in Prepared<Acornima.Ast.Script> prepared)
    {
        if (!_target.IsPaused)
        {
            return _target.Engine.Evaluate(prepared);
        }

        try
        {
            return _target.Engine.Debugger.Evaluate(prepared);
        }
        catch (DebugEvaluationException exception)
        {
            throw Unwrap(exception);
        }
    }

    /// <summary>
    /// The failure a client asked about, out of the debugger's wrapper.
    /// </summary>
    /// <remarks>
    /// A parse failure comes out as the parse failure, so <c>Runtime.evaluate</c> of a malformed expression
    /// answers the same shape whether or not the engine happened to be paused.
    /// </remarks>
    private static Exception Unwrap(DebugEvaluationException exception)
    {
        return exception.InnerException switch
        {
            JavaScriptException javaScript => javaScript,
            Acornima.ParseErrorException parse => new ProtocolException(parse.Message, parse),
            _ => new ProtocolException(exception.Message, exception),
        };
    }

    /// <summary>
    /// Answers when the promise settles, by attaching reactions to it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Never by draining.</b> This runs inside an event-loop job and the pump's re-entrancy guard forbids
    /// a nested drain, so the command completes from the job that runs the reaction — on this same thread,
    /// with the value still in the engine's hands. What crosses back afterwards is a finished record.
    /// </para>
    /// <para>
    /// <b>A paused engine settles nothing</b>, because settling means running a reaction and the engine is
    /// stopped inside a statement of something else. Attaching one would answer the command only after the
    /// resume — long after the client stopped waiting — so the promise is described as it stands, pending,
    /// and the client can ask again once it has resumed.
    /// </para>
    /// </remarks>
    private ValueTask<TResponse> Settled<TResponse>(
        JsValue promise,
        RemoteObjectRequest request,
        Func<RemoteObject, ExceptionDetails?, TResponse> build)
    {
        if (_target.IsPaused)
        {
            return new ValueTask<TResponse>(build(_objects.Describe(promise, request), null));
        }

        var engine = _target.Engine;
        var then = promise.Get("then");
        if (!then.IsCallable())
        {
            return new ValueTask<TResponse>(build(_objects.Describe(promise, request), null));
        }

        var completion = new TaskCompletionSource<TResponse>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Both reactions catch everything, and that is not defensive noise: they run as event-loop jobs, so
        // anything they let escape erupts out of the host's own pump. Describing the settled value can fail
        // -- a by-value result the protocol has no form for is a refusal -- and the client is what should
        // hear about it.
        var onFulfilled = new ClrFunction(engine, "", (_, arguments) =>
        {
            try
            {
                var result = arguments.Length > 0 ? arguments[0] : JsValue.Undefined;
                completion.TrySetResult(build(_objects.Describe(result, request), null));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }

            return JsValue.Undefined;
        });

        var onRejected = new ClrFunction(engine, "", (_, arguments) =>
        {
            try
            {
                var reason = arguments.Length > 0 ? arguments[0] : JsValue.Undefined;
                var details = _objects.Rejection(reason, NextExceptionId(), MainExecutionContextId);
                completion.TrySetResult(build(_objects.Describe(reason, Thrown(request, reason)), details));
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }

            return JsValue.Undefined;
        });

        engine.Call(then, promise, [onFulfilled, onRejected]);
        return new ValueTask<TResponse>(completion.Task);
    }

    private EvaluateResponse EvaluateFailure(JavaScriptException exception, in RemoteObjectRequest request)
    {
        // The protocol wants both: the details a client renders in its console, and the thrown value itself
        // as the command's result, which is what a client inspects.
        return new EvaluateResponse
        {
            Result = _objects.Describe(exception.Error, Thrown(request, exception.Error)),
            ExceptionDetails = _objects.Exception(exception, NextExceptionId(), MainExecutionContextId),
        };
    }

    private CallFunctionOnResponse CallFailure(JavaScriptException exception, in RemoteObjectRequest request)
    {
        return new CallFunctionOnResponse
        {
            Result = _objects.Describe(exception.Error, Thrown(request, exception.Error)),
            ExceptionDetails = _objects.Exception(exception, NextExceptionId(), MainExecutionContextId),
        };
    }

    /// <summary>
    /// What a thrown value is described as. A client that asked for the <i>result</i> by value did not ask
    /// for the error object by value, and an error has no useful JSON form; it gets a handle instead.
    /// </summary>
    private static RemoteObjectRequest Thrown(in RemoteObjectRequest request, JsValue thrown)
    {
        return thrown.IsObject()
            ? request with { ByValue = false, Addressable = true }
            : request;
    }

    /// <summary>Refuses a context that is not the one context an engine target has.</summary>
    private static void RequireMainContext(int? contextId)
    {
        if (contextId is { } id && id != MainExecutionContextId)
        {
            // Chrome's wording, which several clients match on to decide a context went away rather than
            // that the call was wrong.
            Throw.ServerError("Cannot find context with specified id");
        }
    }

    private int NextExceptionId() => ++_exceptionId;

    private static JsonElement AuxData
    {
        get
        {
            using var document = JsonDocument.Parse("""{"isDefault":true,"type":"default"}""");
            return document.RootElement.Clone();
        }
    }
}
