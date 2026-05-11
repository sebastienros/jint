using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintIfStatement : JintStatement<IfStatement>
{
    private readonly ProbablyBlockStatement _statementConsequent;
    private readonly JintExpression _test;
    private readonly ProbablyBlockStatement? _alternate;
    private readonly bool _consequentIsFunctionDecl;
    private readonly bool _alternateIsFunctionDecl;

    public JintIfStatement(IfStatement statement) : base(statement)
    {
        _statementConsequent = new ProbablyBlockStatement(statement.Consequent);
        _test = JintExpression.Build(statement.Test);
        _alternate = statement.Alternate != null ? new ProbablyBlockStatement(statement.Alternate) : null;
        _consequentIsFunctionDecl = statement.Consequent.Type == NodeType.FunctionDeclaration;
        _alternateIsFunctionDecl = statement.Alternate?.Type == NodeType.FunctionDeclaration;
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        Completion result;
        var suspensionNode = GetSuspensionNode(context.Engine.ExecutionContext.Suspendable);
        if (suspensionNode is not null && IsNodeInsideRange(suspensionNode, _statement.Consequent.Range))
        {
            result = _statementConsequent.Execute(context);
        }
        else if (suspensionNode is not null
            && _statement.Alternate is not null
            && IsNodeInsideRange(suspensionNode, _statement.Alternate.Range))
        {
            result = _alternate!.Value.Execute(context);
        }
        else
        {
            var testResult = _test.GetBooleanValue(context);
            if (context.IsSuspended())
            {
                var suspendable = context.Engine.ExecutionContext.Suspendable;
                var suspendedValue = suspendable?.SuspendedValue ?? JsValue.Undefined;
                result = new Completion(CompletionType.Return, suspendedValue, _statement);
            }
            else if (testResult)
            {
                // B.3.2/B.3.3: IfStatement function declarations need runtime AnnexB handling
                if (_consequentIsFunctionDecl && !StrictModeScope.IsStrictModeCode)
                {
                    result = ExecuteAnnexBFunctionDeclaration(context, (FunctionDeclaration) _statement.Consequent);
                }
                else
                {
                    result = _statementConsequent.Execute(context);
                }
            }
            else if (_alternate != null)
            {
                if (_alternateIsFunctionDecl && !StrictModeScope.IsStrictModeCode)
                {
                    result = ExecuteAnnexBFunctionDeclaration(context, (FunctionDeclaration) _statement.Alternate!);
                }
                else
                {
                    result = _alternate.Value.Execute(context);
                }
            }
            else
            {
                result = Completion.Empty();
            }
        }

        return result.UpdateEmpty(JsValue.Undefined);
    }

    /// <summary>
    /// B.3.4: When an IfStatement's consequent or alternate is a FunctionDeclaration
    /// in sloppy mode, treat it as if wrapped in a block: create a block-scoped binding
    /// for the function, instantiate the function object in that scope, and copy to var scope.
    /// </summary>
    private static Completion ExecuteAnnexBFunctionDeclaration(EvaluationContext context, FunctionDeclaration funcDecl)
    {
        var fn = funcDecl.Id?.Name;
        if (fn is not null)
        {
            var engine = context.Engine;
            var executionContext = engine.ExecutionContext;
            var varEnv = executionContext.VariableEnvironment;
            var outerLexEnv = executionContext.LexicalEnvironment;

            // Create a new lexical environment (implicit block scope) for the function declaration
            var blockEnv = JintEnvironment.NewDeclarativeEnvironment(engine, outerLexEnv);
            blockEnv.CreateMutableBinding(fn, canBeDeleted: false);

            // Instantiate the function in the block scope
            var definition = new JintFunctionDefinition(funcDecl);
            var fo = engine.Realm.Intrinsics.Function.InstantiateFunctionObject(definition, blockEnv, executionContext.PrivateEnvironment);
            blockEnv.InitializeBinding(fn, fo, DisposeHint.Normal);

            // Copy to var scope if AnnexB-eligible
            var shouldCopy = false;
            if (executionContext.Function is { _functionDefinition: { } funcDef })
            {
                shouldCopy = funcDef.Initialize().AnnexBFunctionDeclarations?.Contains(funcDecl) == true;
            }
            else if (varEnv is GlobalEnvironment globalEnv)
            {
                shouldCopy = !globalEnv.HasLexicalDeclaration(fn) && globalEnv.HasBinding(fn);
            }
            else
            {
                shouldCopy = varEnv.HasBinding(fn);
            }

            if (shouldCopy)
            {
                varEnv.SetMutableBinding(fn, fo, strict: false);
            }
        }

        return new Completion(CompletionType.Normal, JsEmpty.Instance, funcDecl);
    }
}
