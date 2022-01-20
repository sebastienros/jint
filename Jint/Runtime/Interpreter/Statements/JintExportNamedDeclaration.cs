#nullable enable

using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportNamedDeclaration : JintStatement<ExportNamedDeclaration>
{
    private JintExpression? _declarationExpression;
    private JintStatement? _declarationStatement;
    private ExportedSpecifier[]? _specifiers;

    private sealed record ExportedSpecifier(
        JintExpression Local,
        JintExpression Exported
    );

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

        if (_statement.Specifiers.Count > 0)
        {
            _specifiers = new ExportedSpecifier[_statement.Specifiers.Count];
            ref readonly var statementSpecifiers = ref _statement.Specifiers;
            for (var i = 0; i < statementSpecifiers.Count; i++)
            {
                var statementSpecifier = statementSpecifiers[i];

                _specifiers[i] = new ExportedSpecifier(
                    Local: JintExpression.Build(context.Engine, statementSpecifier.Local),
                    Exported: JintExpression.Build(context.Engine, statementSpecifier.Exported)
                );
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-exports-runtime-semantics-evaluation
    /// </summary>
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var module = context.Engine.GetActiveScriptOrModule().AsModule(context.Engine, context.LastSyntaxNode.Location);

        if (_specifiers != null)
        {
            foreach (var specifier in _specifiers)
            {
                if (specifier.Local is not JintIdentifierExpression local || specifier.Exported is not JintIdentifierExpression exported)
                {
                    ExceptionHelper.ThrowSyntaxError(context.Engine.Realm, "", context.LastSyntaxNode.Location);
                    return default;
                }

                var localKey = local._expressionName.Key.Name;
                var exportedKey = exported._expressionName.Key.Name;
                if (localKey != exportedKey)
                {
                    module._environment.CreateImportBinding(exportedKey, module, localKey);
                }
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