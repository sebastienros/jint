#nullable enable

using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportNamedDeclaration : JintStatement<ExportNamedDeclaration>
{
    private JintExpression? _declarationExpression;
    private JintStatement? _declarationStatement;
    private ExportedSpecifier[]? _specifiers;

    private readonly record struct ExportedSpecifier(string Local, string Exported);

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
                    Local: statementSpecifier.Local.GetKey(context.Engine).AsString(),
                    Exported: statementSpecifier.Exported.GetKey(context.Engine).AsString()
                );
            }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-exports-runtime-semantics-evaluation
    /// </summary>
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var env = (ModuleEnvironmentRecord) context.Engine.ExecutionContext.LexicalEnvironment;

        if (_specifiers != null)
        {
            var module = context.Engine.GetActiveScriptOrModule().AsModule(context.Engine, context.LastSyntaxNode.Location);
            foreach (var specifier in _specifiers)
            {
                var localKey = specifier.Local;
                var exportedKey = specifier.Exported;
                if (localKey != exportedKey)
                {
                    env.CreateImportBinding(exportedKey, module, localKey);
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
