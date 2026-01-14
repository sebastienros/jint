using Jint.Runtime;
using Jint.Runtime.Environments;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintBlockStatement : JintStatement<NestedBlockStatement>
{
    private JintStatementList? _statementList;
    private JintStatement? _singleStatement;
    private DeclarationCache _lexicalDeclarations;

    public JintBlockStatement(NestedBlockStatement blockStatement) : base(blockStatement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _lexicalDeclarations = (DeclarationCache) (_statement.UserData ??= BuildState(_statement));

        if (_statement.Body.Count == 1)
        {
            _singleStatement = Build(_statement.Body[0]);
        }
        else
        {
            _statementList = new JintStatementList(_statement, _statement.Body);
        }
    }

    internal static DeclarationCache BuildState(BlockStatement blockStatement)
    {
        return DeclarationCacheBuilder.Build(blockStatement);
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
        if (_lexicalDeclarations.Declarations.Count > 0)
        {
            if (suspendable is { IsResuming: true }
                && suspendable.Data.TryGet(this, out BlockSuspendData? suspendData)
                && suspendData?.BlockEnvironment is not null)
            {
                blockEnv = suspendData.BlockEnvironment;
                oldEnv = suspendData.OuterEnvironment ?? blockEnv._outerEnv;
                if (!ReferenceEquals(engine.ExecutionContext.LexicalEnvironment, blockEnv))
                {
                    engine.UpdateLexicalEnvironment(blockEnv);
                }
            }
            else
            {
                oldEnv = engine.ExecutionContext.LexicalEnvironment;
                blockEnv = JintEnvironment.NewDeclarativeEnvironment(engine, engine.ExecutionContext.LexicalEnvironment);
                JintStatementList.BlockDeclarationInstantiation(blockEnv, _lexicalDeclarations);
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
}
