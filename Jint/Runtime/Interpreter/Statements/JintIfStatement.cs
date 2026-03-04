using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintIfStatement : JintStatement<IfStatement>
{
    private ProbablyBlockStatement _statementConsequent;
    private JintExpression _test = null!;
    private ProbablyBlockStatement? _alternate;
    private bool _consequentIsFunctionDecl;
    private bool _alternateIsFunctionDecl;

    public JintIfStatement(IfStatement statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        _statementConsequent = new ProbablyBlockStatement(_statement.Consequent);
        _test = JintExpression.Build(_statement.Test);
        _alternate = _statement.Alternate != null ? new ProbablyBlockStatement(_statement.Alternate) : null;
        _consequentIsFunctionDecl = _statement.Consequent.Type == NodeType.FunctionDeclaration;
        _alternateIsFunctionDecl = _statement.Alternate?.Type == NodeType.FunctionDeclaration;
    }

    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        Completion result;
        if (TypeConverter.ToBoolean(_test.GetValue(context)))
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

        return result.UpdateEmpty(JsValue.Undefined);
    }

    /// <summary>
    /// B.3.2/B.3.3: When an IfStatement's consequent or alternate is a FunctionDeclaration
    /// in sloppy mode, create the function object and copy it to the var scope.
    /// Uses the same AnnexB eligibility checks as BlockDeclarationInstantiation.
    /// </summary>
    private static Completion ExecuteAnnexBFunctionDeclaration(EvaluationContext context, FunctionDeclaration funcDecl)
    {
        var fn = funcDecl.Id?.Name;
        if (fn is not null)
        {
            var engine = context.Engine;
            var executionContext = engine.ExecutionContext;
            var varEnv = executionContext.VariableEnvironment;

            // Determine if this function name should be AnnexB-hoisted using the same
            // logic as BlockDeclarationInstantiation (JintStatementList.cs)
            var shouldCopy = false;
            if (executionContext.Function is { _functionDefinition: { } funcDef })
            {
                // Function scope: check AnnexBFunctionNames from B.3.3.1
                shouldCopy = funcDef.Initialize().AnnexBFunctionNames?.Contains(fn) == true;
            }
            else if (varEnv is GlobalEnvironment globalEnv)
            {
                // Global/eval scope: copy if not a lexical declaration and binding exists
                shouldCopy = !globalEnv.HasLexicalDeclaration(fn) && globalEnv.HasBinding(fn);
            }
            else
            {
                // Eval in function scope: copy if var binding exists
                shouldCopy = varEnv.HasBinding(fn);
            }

            if (shouldCopy)
            {
                var lexEnv = executionContext.LexicalEnvironment;
                var definition = new JintFunctionDefinition(funcDecl);
                var fo = engine.Realm.Intrinsics.Function.InstantiateFunctionObject(definition, lexEnv, executionContext.PrivateEnvironment);
                varEnv.SetMutableBinding(fn, fo, strict: false);
            }
        }

        return new Completion(CompletionType.Normal, JsEmpty.Instance, funcDecl);
    }
}
