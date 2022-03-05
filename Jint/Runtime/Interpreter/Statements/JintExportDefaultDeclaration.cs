#nullable enable

using Esprima.Ast;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportDefaultDeclaration : JintStatement<ExportDefaultDeclaration>
{
    private JintClassDeclarationStatement? _classDeclaration;
    private JintFunctionDeclarationStatement? _functionDeclaration;
    private JintExpression? _assignmentExpression;
    private JintExpression? _simpleExpression;

    public JintExportDefaultDeclaration(ExportDefaultDeclaration statement) : base(statement)
    {
    }

    protected override void Initialize(EvaluationContext context)
    {
        if (_statement.Declaration is ClassDeclaration classDeclaration)
        {
            _classDeclaration = new JintClassDeclarationStatement(classDeclaration);
        }
        else if (_statement.Declaration is FunctionDeclaration functionDeclaration)
        {
            _functionDeclaration = new JintFunctionDeclarationStatement(functionDeclaration);
        }
        else if (_statement.Declaration is AssignmentExpression assignmentExpression)
        {
            _assignmentExpression = JintAssignmentExpression.Build(context.Engine, assignmentExpression);
        }
        else
        {
            _simpleExpression = JintExpression.Build(context.Engine, (Expression) _statement.Declaration);
        }
    }

    /// <summary>
    ///  https://tc39.es/ecma262/#sec-exports-runtime-semantics-evaluation
    /// </summary>
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        JsValue value;
        if (_classDeclaration is not null)
        {
            value = _classDeclaration.Execute(context).GetValueOrDefault();
        }     
        else if (_functionDeclaration is not null)
        {
            value = _functionDeclaration.Execute(context).GetValueOrDefault();
        }
        else if (_assignmentExpression is not null)
        {
            value = _assignmentExpression.GetValue(context).GetValueOrDefault();
        }
        else
        {
            value = _simpleExpression!.GetValue(context).GetValueOrDefault();
        }

        if (value is ObjectInstance oi && !oi.HasOwnProperty("name"))
        {
            oi.SetFunctionName("default");
        }
        
        var env = context.Engine.ExecutionContext.LexicalEnvironment;
        InitializeBoundName("*default*", value, env);
        return Completion.Empty();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-initializeboundname
    /// </summary>
    private void InitializeBoundName(string name, JsValue value, EnvironmentRecord? environment)
    {
        if (environment is not null)
        {
            environment.InitializeBinding(name, value);
        }
        else
        {
            ExceptionHelper.ThrowNotImplementedException();
        }
    }
}