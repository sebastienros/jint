using System.Threading;
using Jint.Native;
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
                    // Try to reuse cached environment (only valid for same Engine)
                    var cachedEnv = Interlocked.Exchange(ref blockState._cachedEnv, null);

                    if (cachedEnv is not null && ReferenceEquals(cachedEnv._engine, engine))
                    {
                        // Reuse environment: update outer reference and reset slots
                        cachedEnv._outerEnv = oldEnv;
                        ResetSlots(cachedEnv._slots!, blockState.SlotTemplates!);
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

        // If resuming from a disposal-caused suspension (await-using with null/undefined),
        // skip re-executing the body — the block body already completed, we're just
        // resuming after the implicit Await tick from DisposeResources.
        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out BlockSuspendData? disposeResumeData)
            && disposeResumeData?.DisposalComplete == true)
        {
            suspendable.IsResuming = false;
            suspendable.Data.Clear(this);
            if (oldEnv is not null)
            {
                engine.UpdateLexicalEnvironment(oldEnv);
            }
            return new Completion(CompletionType.Normal, JsValue.Undefined, _statement);
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
            else
            {
                // Return environment to cache for reuse (slots only exist when CanReuseEnvironment=true)
                if (blockEnv._slots is not null)
                {
                    Interlocked.Exchange(ref blockState._cachedEnv, blockEnv);
                }

                blockValue = blockEnv.DisposeResources(blockValue);

                // Per spec Dispose step 3.a: await-using with null/undefined value requires
                // an implicit Await tick. Suspend the async function to introduce the tick.
                if (blockEnv.NeedsAsyncDisposeTick)
                {
                    var asyncFn = engine.ExecutionContext.AsyncFunction;
                    if (asyncFn is not null)
                    {
                        var promise = (JsPromise) engine.Realm.Intrinsics.Promise.PromiseResolve(JsValue.Undefined);

                        asyncFn._lastAwaitNode = this;
                        asyncFn._state = Native.AsyncFunction.AsyncFunctionState.SuspendedAwait;
                        asyncFn._savedContext = engine.ExecutionContext;

                        var onFulfilled = new ClrFunction(engine, "", (_, args) =>
                        {
                            asyncFn._resumeValue = JsValue.Undefined;
                            asyncFn._resumeWithThrow = false;
                            JintAwaitExpression.AsyncFunctionResume(engine, asyncFn);
                            return JsValue.Undefined;
                        }, 1, PropertyFlag.Configurable);

                        PromiseOperations.PerformPromiseThen(engine, promise, onFulfilled, null!, null!);

                        if (suspendable is not null)
                        {
                            var data = suspendable.Data.GetOrCreate<BlockSuspendData>(this);
                            data.BlockEnvironment = blockEnv;
                            data.OuterEnvironment = oldEnv;
                            data.DisposalComplete = true;
                        }
                        if (oldEnv is not null)
                        {
                            engine.UpdateLexicalEnvironment(oldEnv);
                        }
                        return blockValue;
                    }
                }

                suspendable?.Data.Clear(this);
            }
        }

        if (oldEnv is not null)
        {
            engine.UpdateLexicalEnvironment(oldEnv);
        }

        return blockValue;
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

        // Fixed-slot storage for qualifying block scopes (no function/class declarations, 1-16 bindings)
        public readonly Key[]? SlotNames;
        public readonly Binding[]? SlotTemplates;

        /// <summary>
        /// True when the block has slots and no closures capture the block's bindings,
        /// meaning the DeclarativeEnvironment object itself can be reused across iterations.
        /// </summary>
        public readonly bool CanReuseEnvironment;

        // Cached environment for reuse (thread-safe via Interlocked.Exchange)
        public DeclarativeEnvironment? _cachedEnv;

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
                    CanReuseEnvironment = true;
                }
            }
        }
    }
}
