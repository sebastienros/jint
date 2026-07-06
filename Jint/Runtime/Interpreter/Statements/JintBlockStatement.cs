using Jint.Native;
using Jint.Native.AsyncFunction;
using Jint.Native.Disposable;
using Jint.Native.Promise;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintBlockStatement : JintStatement<NestedBlockStatement>
{
    private readonly JintStatementList? _statementList;
    private readonly JintStatement? _singleStatement;
    private readonly BlockState _blockState;

    // Reuse cache for this block's fixed-slot environment. Held on the handler instance — which is built
    // per statement list, i.e. per engine — rather than on the shared BlockState: BlockState lives on the
    // AST node's UserData and is shared by every engine running a prepared script, and an environment roots
    // its creating engine, so caching it there pinned the last-caller engine (issue #2560). Single-threaded
    // like the engine, so no Interlocked or engine-identity check is needed.
    private DeclarativeEnvironment? _cachedEnv;

    public JintBlockStatement(NestedBlockStatement blockStatement) : base(blockStatement)
    {
        _blockState = (BlockState) (blockStatement.UserData ??= BuildState(blockStatement));

        if (blockStatement.Body.Count == 1)
        {
            _singleStatement = Build(blockStatement.Body[0]);
        }
        else
        {
            _statementList = new JintStatementList(blockStatement, blockStatement.Body);
        }
    }

    internal static BlockState BuildState(BlockStatement blockStatement)
    {
        var declarations = DeclarationCacheBuilder.Build(blockStatement);
        return new BlockState(declarations, blockStatement);
    }

    /// <summary>
    /// Optimized for direct access without virtual dispatch.
    /// </summary>
    public Completion ExecuteBlock(EvaluationContext context)
    {
        DeclarativeEnvironment? blockEnv = null;
        Environment? oldEnv = null;
        var engine = context.Engine;
        var suspendable = engine.ExecutionContext.Suspendable;
        var blockState = _blockState;
        if (blockState.Declarations.Count > 0)
        {
            if (suspendable is { IsResuming: true }
                && suspendable.Data.TryGet(this, out BlockSuspendData? suspendData)
                && suspendData?.BlockEnvironment is not null)
            {
                blockEnv = suspendData.BlockEnvironment;
                if (suspendData.OuterEnvironment is null)
                {
                    // OuterEnvironment should be captured on suspension; fall back to current env if missing.
                    oldEnv = engine.ExecutionContext.LexicalEnvironment;
                }
                else
                {
                    oldEnv = suspendData.OuterEnvironment;
                }
                if (!ReferenceEquals(engine.ExecutionContext.LexicalEnvironment, blockEnv))
                {
                    engine.UpdateLexicalEnvironment(blockEnv);
                }
            }
            else
            {
                oldEnv = engine.ExecutionContext.LexicalEnvironment;

                if (blockState.SlotNames is not null)
                {
                    // Try to reuse this handler's cached environment (per engine by construction)
                    var cachedEnv = _cachedEnv;

                    if (cachedEnv is not null)
                    {
                        _cachedEnv = null;
                        // Reuse environment: re-attach the outer reference (slots were already reset
                        // and the outer chain detached when the env was parked).
                        cachedEnv._outerEnv = oldEnv;
                        blockEnv = cachedEnv;
                    }
                    else
                    {
                        // Create new environment with fixed-slot storage
                        blockEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
                        blockEnv._slotNames = blockState.SlotNames;
                        blockEnv._slots = (Binding[]) blockState.SlotTemplates!.Clone();
                    }
                }
                else
                {
                    blockEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
                    JintStatementList.BlockDeclarationInstantiation(blockEnv, blockState.DeclarationCache);
                }

                engine.UpdateLexicalEnvironment(blockEnv);
            }
        }

        Completion blockValue;

        // Resuming from a dispose-driven suspension: don't re-execute the body — advance
        // the dispose state machine from where it suspended.
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out BlockSuspendData? disposeResumeData)
            && disposeResumeData?.DisposeInProgress == true)
        {
            return ResumeDispose(context, blockEnv, oldEnv, disposeResumeData, suspendable);
        }

        if (_singleStatement is not null)
        {
            blockValue = ExecuteSingle(context);
        }
        else
        {
            blockValue = _statementList!.Execute(context);
        }

        if (blockEnv != null)
        {
            if (context.IsSuspended())
            {
                if (suspendable is not null)
                {
                    var data = suspendable.Data.GetOrCreate<BlockSuspendData>(this);
                    data.BlockEnvironment = blockEnv;
                    data.OuterEnvironment = oldEnv;
                }
            }
            else if (!blockEnv.HasDisposeResources)
            {
                // Nothing was registered for dispose (no using/await-using declaration ran), so the
                // dispose state machine is a no-op — finalize inline instead of round-tripping a
                // DisposeStepResult through CompleteDispose. Mirrors its finalize arm exactly.
                suspendable?.Data.Clear(this);
                if (oldEnv is not null)
                {
                    engine.UpdateLexicalEnvironment(oldEnv);

                    if (blockEnv._slots is not null)
                    {
                        blockEnv._outerEnv = null;
                        ResetSlots(blockEnv._slots, _blockState.SlotTemplates!);
                        _cachedEnv = blockEnv;
                    }
                }
                return blockValue;
            }
            else
            {
                return CompleteDispose(
                    context,
                    blockEnv,
                    oldEnv,
                    suspendable,
                    blockEnv.BeginDisposeResources(blockValue));
            }
        }

        if (oldEnv is not null)
        {
            engine.UpdateLexicalEnvironment(oldEnv);
        }

        return blockValue;
    }

    /// <summary>
    /// Resume the dispose state machine after an awaited dispose promise settled.
    /// </summary>
    private Completion ResumeDispose(
        EvaluationContext context,
        DeclarativeEnvironment? blockEnv,
        Environment? oldEnv,
        BlockSuspendData data,
        ISuspendable suspendable)
    {
        var engine = context.Engine;
        // suspendable.IsResuming setter delegates to asyncFn._isResuming, so this
        // single assignment clears both the interface flag and the underlying field.
        suspendable.IsResuming = false;

        var asyncFn = engine.ExecutionContext.AsyncFunction!;
        var awaitResult = asyncFn._resumeValue ?? JsValue.Undefined;
        var awaitThrew = asyncFn._resumeWithThrow;
        asyncFn._resumeValue = null;
        asyncFn._resumeWithThrow = false;
        asyncFn._lastAwaitNode = null;

        // Use the env captured at suspend time, in case the env-setup branch above
        // didn't run on this re-entry (no declarations path).
        blockEnv ??= data.BlockEnvironment!;
        oldEnv ??= data.OuterEnvironment;
        engine.UpdateLexicalEnvironment(blockEnv);

        return CompleteDispose(
            context,
            blockEnv,
            oldEnv,
            suspendable,
            blockEnv.ContinueDisposeResources(awaitResult, awaitThrew));
    }

    /// <summary>
    /// Drives the dispose state machine to completion. For each suspension, suspends
    /// the surrounding async function (via the same machinery as <c>await</c>); when
    /// the dispose promise settles, the function resumes and the block re-enters
    /// <see cref="ExecuteBlock"/> with the dispose-in-progress flag set.
    /// </summary>
    private Completion CompleteDispose(
        EvaluationContext context,
        DeclarativeEnvironment blockEnv,
        Environment? oldEnv,
        ISuspendable? suspendable,
        DisposeStepResult step)
    {
        var engine = context.Engine;

        while (!step.IsDone)
        {
            var pending = step.PendingPromise!;
            var asyncFn = engine.ExecutionContext.AsyncFunction;

            if (asyncFn is null)
            {
                // Non-async context fallback. `await using` is syntactically only valid
                // in async, so this path is reached only by unusual hosts. Sync wait.
                JsValue resolved;
                bool threw = false;
                try
                {
                    resolved = pending.UnwrapIfPromise(engine.Options.Constraints.PromiseTimeout);
                }
                catch (PromiseRejectedException e)
                {
                    resolved = e.RejectedValue;
                    threw = true;
                }
                step = blockEnv.ContinueDisposeResources(resolved, threw);
                continue;
            }

            SetupDisposeSuspension(engine, asyncFn, pending);

            var data = suspendable!.Data.GetOrCreate<BlockSuspendData>(this);
            data.BlockEnvironment = blockEnv;
            data.OuterEnvironment = oldEnv;
            data.DisposeInProgress = true;

            if (oldEnv is not null)
            {
                engine.UpdateLexicalEnvironment(oldEnv);
            }
            return new Completion(CompletionType.Normal, JsValue.Undefined, _statement);
        }

        // Dispose finished — finalize: restore outer env, cache env, return.
        suspendable?.Data.Clear(this);
        if (oldEnv is not null)
        {
            engine.UpdateLexicalEnvironment(oldEnv);

            // Park only after leaving the block, and reset at park time so the cached env doesn't
            // root the completed call's scope chain and the last run's binding values until the
            // block next executes.
            if (blockEnv._slots is not null)
            {
                blockEnv._outerEnv = null;
                ResetSlots(blockEnv._slots, _blockState.SlotTemplates!);
                _cachedEnv = blockEnv;
            }
        }
        return step.CompletedResult;
    }

    /// <summary>
    /// Mirror of <see cref="JintAwaitExpression.SuspendForAwait"/> for the dispose path:
    /// suspends the async function on the pending dispose promise, with handlers that
    /// resume the function (which re-enters this block via <see cref="ResumeDispose"/>).
    /// </summary>
    private void SetupDisposeSuspension(Engine engine, AsyncFunctionInstance asyncFn, JsValue pendingPromise)
    {
        var promise = pendingPromise as JsPromise
            ?? (JsPromise) engine.Realm.Intrinsics.Promise.PromiseResolve(pendingPromise);

        asyncFn._lastAwaitNode = this;
        asyncFn._state = AsyncFunctionState.SuspendedAwait;
        asyncFn._savedContext = engine.ExecutionContext;

        var onFulfilled = new ClrFunction(engine, "", (_, args) =>
        {
            asyncFn._resumeValue = args.At(0);
            asyncFn._resumeWithThrow = false;
            JintAwaitExpression.AsyncFunctionResume(engine, asyncFn);
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        var onRejected = new ClrFunction(engine, "", (_, args) =>
        {
            asyncFn._resumeValue = args.At(0);
            asyncFn._resumeWithThrow = true;
            JintAwaitExpression.AsyncFunctionResume(engine, asyncFn);
            return JsValue.Undefined;
        }, 1, PropertyFlag.Configurable);

        PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, onRejected, null!);
    }

    private Completion ExecuteSingle(EvaluationContext context)
    {
        Completion blockValue;
        try
        {
            blockValue = _singleStatement!.Execute(context);
            if (context.Engine._error is not null)
            {
                blockValue = JintStatementList.HandleError(context.Engine, _singleStatement);
            }
        }
        catch (Exception ex)
        {
            if (ex is JintException)
            {
                blockValue = JintStatementList.HandleException(context, ex, _singleStatement);
            }
            else
            {
                var locationNode = (Node?) context.Engine._lastSyntaxElement ?? _singleStatement!._statement;
                ExceptionDataHelper.TryAttachJavaScriptLocation(ex, context.Engine, locationNode.Location);
                throw;
            }
        }

        // Check for generator suspension
        var gen = context.Engine.ExecutionContext.Generator;
        if (context.IsSuspended())
        {
            var suspendedValue = gen?._suspendedValue ?? blockValue.Value;
            return new Completion(CompletionType.Return, suspendedValue, _singleStatement!._statement);
        }

        // Check for generator return request
        if (gen?._returnRequested == true)
        {
            var returnValue = gen._suspendedValue ?? blockValue.Value;
            return new Completion(CompletionType.Return, returnValue, _singleStatement!._statement);
        }

        return blockValue;
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        return ExecuteBlock(context);
    }

    /// <summary>
    /// Reset the slots of a reused block environment to the pre-computed templates.
    /// Hand-rolled small-array fast path: typical block scopes hold 1-3 bindings, where the
    /// JIT can unroll this loop and avoid the Span construction + generic copy of CopyTo.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void ResetSlots(Binding[] slots, Binding[] templates)
    {
        var len = slots.Length;
        if (len == templates.Length && len <= 4)
        {
            for (var i = 0; i < len; i++)
            {
                slots[i] = templates[i];
            }
        }
        else
        {
            templates.AsSpan().CopyTo(slots);
        }
    }

    /// <summary>
    /// Pre-computed block scope state, cached on AST node UserData.
    /// </summary>
    internal sealed class BlockState
    {
        public readonly DeclarationCache DeclarationCache;
        public readonly List<ScopedDeclaration> Declarations;

        // Fixed-slot storage for qualifying block scopes (no function/class declarations, no escaping
        // closures, 1-16 bindings). Non-null SlotNames is also the gate for reusing the environment
        // object itself; the env cache lives on the per-engine JintBlockStatement handler instance —
        // never here: BlockState is shared across engines via AST UserData, and a cached environment
        // would root its creating engine (issue #2560).
        public readonly Key[]? SlotNames;
        public readonly Binding[]? SlotTemplates;

        public BlockState(DeclarationCache declarationCache, BlockStatement blockStatement)
        {
            DeclarationCache = declarationCache;
            Declarations = declarationCache.Declarations;

            // Determine slot eligibility: no function/class declarations, all variable declarations, 1-16 bindings
            if (declarationCache.AllLexicalScoped && declarationCache.Declarations.Count > 0)
            {
                var totalBindings = 0;
                var eligible = true;
                foreach (var decl in declarationCache.Declarations)
                {
                    if (decl.Declaration is not VariableDeclaration)
                    {
                        eligible = false;
                        break;
                    }
                    totalBindings += decl.BoundNames.Length;
                }

                if (eligible && totalBindings is > 0 and <= 16
                    && !JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(blockStatement))
                {
                    var slotNames = new Key[totalBindings];
                    var slotTemplates = new Binding[totalBindings];
                    var idx = 0;
                    foreach (var decl in declarationCache.Declarations)
                    {
                        foreach (var bn in decl.BoundNames)
                        {
                            slotNames[idx] = bn;
                            slotTemplates[idx] = decl.IsConstantDeclaration
                                ? new Binding(null!, canBeDeleted: false, mutable: false, strict: true)
                                : new Binding(null!, canBeDeleted: false, mutable: true, strict: false);
                            idx++;
                        }
                    }
                    SlotNames = slotNames;
                    SlotTemplates = slotTemplates;
                }
            }
        }
    }
}
