using System.Threading;
using Jint.Runtime.Environments;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintBlockStatement : JintStatement<NestedBlockStatement>
{
    private JintStatementList? _statementList;
    private JintStatement? _singleStatement;
    private BlockState _blockState = null!;

    public JintBlockStatement(NestedBlockStatement blockStatement) : base(blockStatement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _blockState = (BlockState) (_statement.UserData ??= BuildState(_statement));

        if (_statement.Body.Count == 1)
        {
            _singleStatement = Build(_statement.Body[0]);
        }
        else
        {
            _statementList = new JintStatementList(_statement, _statement.Body);
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
        if (_statementList is null && _singleStatement is null)
        {
            Initialize(context);
        }

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
                    var cachedEnv = blockState.CanReuseEnvironment
                        ? Interlocked.Exchange(ref blockState._cachedEnv, null)
                        : null;

                    if (cachedEnv is not null && ReferenceEquals(cachedEnv._engine, engine))
                    {
                        // Reuse environment: update outer reference and reset slots
                        cachedEnv._outerEnv = oldEnv;
                        blockState.SlotTemplates.AsSpan().CopyTo(cachedEnv._slots);
                        blockEnv = cachedEnv;
                    }
                    else
                    {
                        // Create new environment with fixed-slot storage
                        blockEnv = JintEnvironment.NewDeclarativeEnvironment(engine, oldEnv);
                        blockEnv._slotNames = blockState.SlotNames;
                        var cached = Interlocked.Exchange(ref blockState._cachedSlots, null);
                        if (cached is not null)
                        {
                            blockState.SlotTemplates.AsSpan().CopyTo(cached);
                            blockEnv._slots = cached;
                        }
                        else
                        {
                            blockEnv._slots = (Binding[]) blockState.SlotTemplates!.Clone();
                        }
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
                // Return environment or slot array to cache for reuse
                if (blockState.CanReuseEnvironment && blockEnv._slots is not null)
                {
                    Interlocked.Exchange(ref blockState._cachedEnv, blockEnv);
                }
                else if (blockState.SlotNames is not null && blockEnv._slots is not null)
                {
                    Interlocked.Exchange(ref blockState._cachedSlots, blockEnv._slots);
                }

                blockValue = blockEnv.DisposeResources(blockValue);
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

        // Cached slot array and environment for reuse (thread-safe via Interlocked.Exchange)
        public Binding[]? _cachedSlots;
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

                if (eligible && totalBindings is > 0 and <= 16)
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

                    // Environment can be reused when no closures capture block bindings
                    CanReuseEnvironment = !JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(blockStatement);
                }
            }
        }
    }
}
