#nullable enable

using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportNamedDeclaration : JintStatement<ExportNamedDeclaration>
{
    private JintExpression? _declarationExpression;
    private JintStatement? _declarationStatement;

    public JintExportNamedDeclaration(ExportNamedDeclaration statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        if (_statement.Declaration != null)
        {
            switch (_statement.Declaration)
            {
                case Expression e:
                    _declarationExpression = JintExpression.Build(context.Engine, e);
                    break;
                case Statement s:
                    _declarationStatement = Build(s);
                    break;
                default:
                    ExceptionHelper.ThrowNotSupportedException($"Statement {_statement.Declaration.Type} is not supported in an export declaration.");
                    break;
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-exports-runtime-semantics-evaluation
    /// </summary>
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        if (_declarationStatement != null)
        {
            _declarationStatement.Execute(context);
            return NormalCompletion(Undefined.Instance);
        }

        if (_declarationExpression != null)
        {
            // Named exports don't require anything more since the values are available in the lexical context
            return _declarationExpression.GetValue(context);
        }

        return NormalCompletion(Undefined.Instance);
    }
}
