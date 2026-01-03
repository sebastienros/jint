using Jint.Native;
using Jint.Native.AsyncFunction;
using Jint.Native.Function;
using Jint.Runtime.Interpreter.Expressions;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Runtime.Interpreter.Statements;

internal sealed class JintExportDefaultDeclaration : JintStatement<ExportDefaultDeclaration>
{
    private ClassDefinition? _classDefinition;
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
            _classDefinition = new ClassDefinition(className: classDeclaration.Id?.Name ?? "default", classDeclaration.SuperClass, classDeclaration.Body);
        }
        else if (_statement.Declaration is FunctionDeclaration functionDeclaration)
        {
            _functionDeclaration = new JintFunctionDeclarationStatement(functionDeclaration);
        }
        else if (_statement.Declaration is AssignmentExpression assignmentExpression)
        {
            _assignmentExpression = JintAssignmentExpression.Build(assignmentExpression);
        }
        else
        {
            _simpleExpression = JintExpression.Build((Expression) _statement.Declaration);
        }
    }

    /// <summary>
    ///  https://tc39.es/ecma262/#sec-exports-runtime-semantics-evaluation
    /// </summary>
    protected override Completion ExecuteInternal(EvaluationContext context)
    {
        var env = context.Engine.ExecutionContext.LexicalEnvironment;
        var asyncFn = context.Engine.ExecutionContext.AsyncFunction;

        // For function/class declarations, the binding is already initialized in SourceTextModule.InitializeEnvironment
        // Skip if already bound AND we're not resuming from an async suspension
        if (env.HasBinding("*default*") && (asyncFn is null || !asyncFn._isResuming))
        {
            return Completion.Empty();
        }

        JsValue value;
        if (_classDefinition is not null)
        {
            value = _classDefinition.BuildConstructor(context, env);
            var classBinding = _classDefinition._className;
            if (classBinding != null)
            {
                env.CreateMutableBinding(classBinding);
                env.InitializeBinding(classBinding, value, DisposeHint.Normal);
            }
        }
        else if (_functionDeclaration is not null)
        {
            value = _functionDeclaration.Execute(context).GetValueOrDefault();
        }
        else if (_assignmentExpression is not null)
        {
            value = _assignmentExpression.GetValue(context);
        }
        else
        {
            value = _simpleExpression!.GetValue(context);
        }

        // Check if we suspended at an await - don't initialize yet
        if (asyncFn?._state == AsyncFunctionState.SuspendedAwait)
        {
            return Completion.Empty();
        }

        if (value is Function functionInstance
            && string.IsNullOrWhiteSpace(functionInstance._nameDescriptor?._value?.ToString()))
        {
            functionInstance.SetFunctionName("default");
        }

        env.InitializeBinding("*default*", value, DisposeHint.Normal);
        return Completion.Empty();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-initializeboundname
    /// </summary>
    private static void InitializeBoundName(string name, JsValue value, Environment? environment)
    {
        if (environment is not null)
        {
            environment.InitializeBinding(name, value, DisposeHint.Normal);
        }
        else
        {
            Throw.NotImplementedException();
        }
    }
}
