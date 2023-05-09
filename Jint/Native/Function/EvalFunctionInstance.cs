using Esprima;
using Esprima.Ast;
using Esprima.Utils;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Native.Function;

internal sealed class EvalFunctionInstance : FunctionInstance
{
    private static readonly JsString _functionName = new("eval");

    private static readonly ParserOptions _parserOptions = ParserOptions.Default with { Tolerant = true };
    private readonly JavaScriptParser _parser = new(_parserOptions);

    public EvalFunctionInstance(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype)
        : base(
            engine,
            realm,
            _functionName,
            StrictModeScope.IsStrictModeCode ? FunctionThisMode.Strict : FunctionThisMode.Global)
    {
        _prototype = functionPrototype;
        _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
    }

    protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
    {
        var callerRealm = _engine.ExecutionContext.Realm;
        var x = arguments.At(0);
        return PerformEval(x, callerRealm, false, false);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-performeval
    /// </summary>
    public JsValue PerformEval(JsValue x, Realm callerRealm, bool strictCaller, bool direct)
    {
        if (!x.IsString())
        {
            return x;
        }

        var evalRealm = _realm;
        _engine._host.EnsureCanCompileStrings(callerRealm, evalRealm);

        var inFunction = false;
        var inMethod = false;
        var inDerivedConstructor = false;
        var inClassFieldInitializer = false;

        if (direct)
        {
            var thisEnvRec = _engine.ExecutionContext.GetThisEnvironment();
            if (thisEnvRec is FunctionEnvironmentRecord functionEnvironmentRecord)
            {
                var F = functionEnvironmentRecord._functionObject;
                inFunction = true;
                inMethod = thisEnvRec.HasSuperBinding();

                if (F._constructorKind == ConstructorKind.Derived)
                {
                    inDerivedConstructor = true;
                }

                var classFieldInitializerName = (F as ScriptFunctionInstance)?._classFieldInitializerName;
                if (!string.IsNullOrEmpty(classFieldInitializerName?.ToString()))
                {
                    inClassFieldInitializer = true;
                }
            }
        }

        Script? script = null;
        try
        {
            script = _parser.ParseScript(x.ToString(), strict: strictCaller);
        }
        catch (ParserException e)
        {
            if (e.Description == Messages.InvalidLHSInAssignment)
            {
                ExceptionHelper.ThrowReferenceError(callerRealm, (string?) null);
            }
            else
            {
                ExceptionHelper.ThrowSyntaxError(callerRealm, e.Message);
            }
        }

        var body = script.Body;
        if (body.Count == 0)
        {
            return Undefined;
        }

        var analyzer = new EvalScriptAnalyzer();
        analyzer.Visit(script);
        if (!inFunction)
        {
            // if body Contains NewTarget, throw a SyntaxError exception.
            if (analyzer._containsNewTarget)
            {
                ExceptionHelper.ThrowSyntaxError(evalRealm, "new.target expression is not allowed here");
            }
        }

        if (!inMethod)
        {
            // if body Contains SuperProperty, throw a SyntaxError exception.
            if (analyzer._containsSuperProperty)
            {
                ExceptionHelper.ThrowSyntaxError(evalRealm, "'super' keyword unexpected here");
            }
        }

        if (!inDerivedConstructor)
        {
            // if body Contains SuperCall, throw a SyntaxError exception.
            if (analyzer._containsSuperCall)
            {
                ExceptionHelper.ThrowSyntaxError(evalRealm, "'super' keyword unexpected here");
            }
        }

        if (inClassFieldInitializer)
        {
            // if ContainsArguments of body is true, throw a SyntaxError exception.
            if (analyzer._containsArguments)
            {
                ExceptionHelper.ThrowSyntaxError(evalRealm, "'arguments' is not allowed in class field initializer or static initialization block");
            }
        }

        var strictEval = script.Strict || _engine._isStrict;
        var ctx = _engine.ExecutionContext;

        using (new StrictModeScope(strictEval))
        {
            EnvironmentRecord lexEnv;
            EnvironmentRecord varEnv;
            PrivateEnvironmentRecord? privateEnv;
            if (direct)
            {
                lexEnv = JintEnvironment.NewDeclarativeEnvironment(_engine, ctx.LexicalEnvironment);
                varEnv = ctx.VariableEnvironment;
                privateEnv = ctx.PrivateEnvironment;
            }
            else
            {
                lexEnv = JintEnvironment.NewDeclarativeEnvironment(_engine, evalRealm.GlobalEnv);
                varEnv = evalRealm.GlobalEnv;
                privateEnv = null;
            }

            if (strictEval)
            {
                varEnv = lexEnv;
            }

            // If ctx is not already suspended, suspend ctx.

            Engine.EnterExecutionContext(lexEnv, varEnv, evalRealm, privateEnv);

            try
            {
                Engine.EvalDeclarationInstantiation(script, varEnv, lexEnv, privateEnv, strictEval);

                var statement = new JintScript(script);
                var result = statement.Execute(_engine._activeEvaluationContext!);
                var value = result.GetValueOrDefault();

                if (result.Type == CompletionType.Throw)
                {
                    ExceptionHelper.ThrowJavaScriptException(_engine, value, result);
                    return null!;
                }
                else
                {
                    return value;
                }
            }
            finally
            {
                Engine.LeaveExecutionContext();
            }
        }
    }

    private sealed class EvalScriptAnalyzer : AstVisitor
    {
        public bool _containsArguments;
        public bool _containsNewTarget;
        public bool _containsSuperCall;
        public bool _containsSuperProperty;

        protected override object VisitIdentifier(Identifier identifier)
        {
            _containsArguments |= identifier.Name == "arguments";
            return identifier;
        }

        protected override object VisitMetaProperty(MetaProperty metaProperty)
        {
            _containsNewTarget |= metaProperty.Meta.Name == "new" && metaProperty.Property.Name == "target";
            return metaProperty;
        }

        protected override object? VisitMemberExpression(MemberExpression memberExpression)
        {
            _containsSuperProperty |= memberExpression.Object.Type == Nodes.Super;
            return base.VisitMemberExpression(memberExpression);
        }

        protected override object? VisitCallExpression(CallExpression callExpression)
        {
            _containsSuperCall |= callExpression.Callee.Type == Nodes.Super;
            return base.VisitCallExpression(callExpression);
        }
    }
}
