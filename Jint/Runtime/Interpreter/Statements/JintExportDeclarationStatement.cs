using System;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

#nullable enable

namespace Jint.Runtime.Interpreter.Statements;

internal abstract class JintExportDeclarationStatement<T> : JintStatement<T> where T : ExportDeclaration
{
    private JintExpression? _declarationExpression;
    private JintStatement? _declarationStatement;

    protected JintExportDeclarationStatement(T statement) : base(statement)
    {
    }

    protected void InitializeDeclaration(Engine engine, StatementListItem? declaration)
    {
        switch (declaration)
        {
            case null:
                return;
            case Expression e:
                _declarationExpression = JintExpression.Build(engine, e);
                break;
            case Statement s:
                _declarationStatement = Build(s);
                break;
            default:
                throw new NotSupportedException();
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-exports-runtime-semantics-evaluation
    /// </summary>
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        // TODO: This will be required for default exports
        // var module = context.Engine.GetActiveScriptOrModule() as JsModule;
        // if (module == null) throw new JavaScriptException("Export can only be used in a module");

        if (_declarationStatement != null)
        {
            // TODO: Not tested
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