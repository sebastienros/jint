#nullable enable

using System;
using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;
using Jint.Runtime.Modules;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportNamedDeclaration : JintStatement<ExportNamedDeclaration>
{
    private JintExpression? _declarationExpression;
    private JintStatement? _declarationStatement;
    private ExportedSpecifier[]? _specifiers;

    private class ExportedSpecifier
    {
        public JintExpression Local = null!;
        public JintExpression Exported = null!;
    }

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
                    throw new NotSupportedException();
            }
        }

        if (_statement.Specifiers.Count > 0)
        {
            _specifiers = new ExportedSpecifier[_statement.Specifiers.Count];
            for (var i = 0; i < _statement.Specifiers.Count; i++)
            {
                _specifiers[i] = new ExportedSpecifier
                {
                    Local = JintExpression.Build(context.Engine, _statement.Specifiers[i].Local),
                    Exported = JintExpression.Build(context.Engine, _statement.Specifiers[i].Exported),
                };
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-exports-runtime-semantics-evaluation
    /// </summary>
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var module = context.Engine.GetActiveScriptOrModule() as JsModule;
        if (module == null) throw new JavaScriptException("Export can only be used in a module");

        if (_specifiers != null)
        {
            for (var i = 0; i < _specifiers.Length; i++)
            {
                var specifier = _specifiers[i];
                var local = (specifier.Local as JintIdentifierExpression)?._expressionName.Key ?? throw new NotSupportedException("Renamed local export must be an identifier");
                var exported = (specifier.Exported as JintIdentifierExpression)?._expressionName.Key ?? throw new NotSupportedException("Renamed export must be an identifier");
                module._environment.CreateImportBinding(exported.Name, module, local.Name);
            }
        }

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