using System.Globalization;
using System.Text.Json;
using Jint.DevTools.Protocol;
using Jint.DevTools.Protocol.Runtime;
using Jint.DevTools.Session;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;
using JsonParser = Jint.Native.Json.JsonParser;

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
internal sealed partial class RuntimeDomain : RuntimeDomainBase, IBindingListener
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
    protected override ValueTask OnEnabledAsync(CommandContext context)
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

        return EmitAsync(RuntimeEvents.ExecutionContextCreated(created), context.CancellationToken);
    }

    /// <inheritdoc/>
    protected override ValueTask<EmptyResult> RunIfWaitingForDebuggerAsync(EmptyParameters parameters, CommandContext context)
    {
        _target.Dispatcher.ReleaseDebuggerWait();
        return new ValueTask<EmptyResult>(EmptyResult.Instance);
    }

    /// <summary>
    /// Answers nothing, and truthfully: this package keeps no console buffer to discard.
    /// </summary>
    /// <remarks>
    /// Answered rather than refused because clients send it while tidying up and a <c>-32601</c> there is
    /// noise in a client's log about a state it does not have.
    /// </remarks>
    protected override ValueTask<EmptyResult> DiscardConsoleEntriesAsync(EmptyParameters parameters, CommandContext context)
    {
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
            value = _target.Engine.Evaluate(parameters.Expression);
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
            function = engine.Evaluate(_target.CompiledScripts.Declaration(parameters.FunctionDeclaration));
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

        var arguments = Arguments(parameters.Arguments);

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
                    Url = string.IsNullOrEmpty(parameters.SourceURL) ? null : parameters.SourceURL,
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
    /// Answers when the promise settles, by attaching reactions to it.
    /// </summary>
    /// <remarks>
    /// <b>Never by draining.</b> This runs inside an event-loop job and the pump's re-entrancy guard forbids
    /// a nested drain, so the command completes from the job that runs the reaction — on this same thread,
    /// with the value still in the engine's hands. What crosses back afterwards is a finished record.
    /// </remarks>
    private ValueTask<TResponse> Settled<TResponse>(
        JsValue promise,
        RemoteObjectRequest request,
        Func<RemoteObject, ExceptionDetails?, TResponse> build)
    {
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

    /// <summary>Turns the client's argument list into the engine's, resolving every handle it names.</summary>
    private JsValue[] Arguments(CallArgument[]? arguments)
    {
        if (arguments is null || arguments.Length == 0)
        {
            return [];
        }

        var values = new JsValue[arguments.Length];
        for (var i = 0; i < arguments.Length; i++)
        {
            values[i] = Argument(arguments[i]);
        }

        return values;
    }

    private JsValue Argument(CallArgument argument)
    {
        if (argument.ObjectId is { } objectId)
        {
            return _target.RemoteObjects.Resolve(objectId);
        }

        if (argument.UnserializableValue is { } unserializable)
        {
            return Unserializable(unserializable);
        }

        if (argument.Value is not { } value)
        {
            // All three absent is how a client spells `undefined`, which is the one value the protocol has
            // no member for.
            return JsValue.Undefined;
        }

        // The engine's own JSON reader, which builds native arrays and objects and runs no script: there is
        // no reviver, so nothing the client sent can execute on the way in.
        return new JsonParser(_target.Engine).Parse(value.GetRawText());
    }

    private static JsValue Unserializable(string text) => text switch
    {
        "NaN" => double.NaN,
        "Infinity" => double.PositiveInfinity,
        "-Infinity" => double.NegativeInfinity,
        "-0" => JsNumber.Create(-0d),
        _ => BigIntOrRefusal(text),
    };

    private static JsValue BigIntOrRefusal(string text)
    {
        if (text.Length > 1 && text[^1] == 'n' &&
            System.Numerics.BigInteger.TryParse(text.AsSpan(0, text.Length - 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
        {
            return new JsBigInt(value);
        }

        // Chrome's wording for an unserializableValue it does not recognize.
        return Throw.ServerError<JsValue>("Invalid CallArgument: " + text);
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

    /// <summary>
    /// Refuses the one evaluation parameter this package cannot honour: <c>throwOnSideEffect</c>.
    /// </summary>
    /// <remarks>
    /// The front end sends it for the console's eager evaluation — the grey preview that appears as you
    /// type — and it means "throw rather than run anything observable". Answering it would need a
    /// side-effect analysis of the interpreter, which does not exist; answering the evaluation anyway would
    /// run the very code the client asked not to be run. No recorded client sends it, so the refusal is the
    /// answer, and a front end that gets one simply shows no preview.
    /// </remarks>
    private static void RefuseSideEffectFreeEvaluation(bool? throwOnSideEffect)
    {
        if (throwOnSideEffect == true)
        {
            Throw.ServerError(
                "Side-effect free evaluation is not supported",
                "the engine has no side-effect analysis, so an evaluation that must throw rather than run anything observable cannot be answered");
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
