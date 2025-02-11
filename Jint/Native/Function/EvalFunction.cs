using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Statements;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Native.Function;

public sealed class EvalFunction : Function
{
    private static readonly JsString _functionName = new("eval");

    internal EvalFunction(
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

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        var callerRealm = _engine.ExecutionContext.Realm;
        var x = arguments.At(0);
        return PerformEval(x, callerRealm, false, false);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-performeval
    /// </summary>
    internal JsValue PerformEval(JsValue x, Realm callerRealm, bool strictCaller, bool direct)
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
            if (thisEnvRec is FunctionEnvironment functionEnvironmentRecord)
            {
                var F = functionEnvironmentRecord._functionObject;
                inFunction = true;
                inMethod = thisEnvRec.HasSuperBinding();

                if (F._constructorKind == ConstructorKind.Derived)
                {
                    inDerivedConstructor = true;
                }

                var classFieldInitializerName = (F as ScriptFunction)?._classFieldInitializerName;
                if (!string.IsNullOrEmpty(classFieldInitializerName?.ToString()))
                {
                    inClassFieldInitializer = true;
                }
            }
        }

        Script? script = null;
        var parserOptions = _engine.GetActiveParserOptions();
        var adjustedParserOptions = parserOptions with
        {
            AllowReturnOutsideFunction = false,
            AllowNewTargetOutsideFunction = true,
            AllowSuperOutsideMethod = true,
            // This is a workaround, just makes some tests pass. Actually, we need these checks (done either by the parser or by the runtime).
            // TODO: implement a correct solution
            CheckPrivateFields = false
        };
        var parser = _engine.GetParserFor(adjustedParserOptions);
        script = parser.ParseScriptGuarded(_engine.Realm, x.ToString(), strict: strictCaller);

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
            Environment lexEnv;
            Environment varEnv;
            PrivateEnvironment? privateEnv;
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
                var context = _engine._activeEvaluationContext ?? new EvaluationContext(_engine);
                var result = statement.Execute(context);

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
            _containsArguments |= string.Equals(identifier.Name, "arguments", StringComparison.Ordinal);
            return identifier;
        }

        protected override object VisitMetaProperty(MetaProperty metaProperty)
        {
            _containsNewTarget |= string.Equals(metaProperty.Meta.Name, "new", StringComparison.Ordinal) && string.Equals(metaProperty.Property.Name, "target", StringComparison.Ordinal);
            return metaProperty;
        }

        protected override object? VisitMemberExpression(MemberExpression memberExpression)
        {
            _containsSuperProperty |= memberExpression.Object.Type == NodeType.Super;
            return base.VisitMemberExpression(memberExpression);
        }

        protected override object? VisitCallExpression(CallExpression callExpression)
        {
            _containsSuperCall |= callExpression.Callee.Type == NodeType.Super;
            return base.VisitCallExpression(callExpression);
        }
    }
}
