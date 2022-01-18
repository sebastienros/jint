#nullable enable

using System;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.Modules;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportNamedDeclarationStatement : JintExportDeclarationStatement<ExportNamedDeclaration>
{
    public JintExportNamedDeclarationStatement(ExportNamedDeclaration statement) : base(statement)
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
                    throw new NotSupportedException();
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-exports-runtime-semantics-evaluation
    /// </summary>
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        // var module = context.Engine.GetActiveScriptOrModule() as JsModule;
        // if (module == null) throw new JavaScriptException("Export can only be used in a module");

        foreach (var specifier in _statement.Specifiers)
        {
            //module._environment.CreateImportBinding();
        }

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