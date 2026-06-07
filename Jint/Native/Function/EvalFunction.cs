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

    // Compilation cache for repeated eval of identical sources (a la V8's compilation cache).
    // Keyed by source + parse strictness (cheap hash); the parse-affecting ParserOptions are
    // validated on hit. Entries hold the parsed AST, the prebuilt statement tree and the
    // static analysis flags. Parse failures are never cached.
    private const int CacheCapacity = 32;
    private const int CacheMaxSourceLength = 32 * 1024;
    private Dictionary<CacheKey, CacheEntry>? _evalCache;

    // Memoized `with`-adjustment of the active parser options (stable instance in steady state).
    private ParserOptions? _lastBaseParserOptions;
    private ParserOptions? _lastAdjustedParserOptions;

    // Two-touch promotion: a source enters the cache only when seen twice, so one-shot eval
    // workloads pay just a failed lookup + key compare instead of an insert per call.
    private CacheKey _probationKey;

    private readonly record struct CacheKey(string Source, bool StrictParse);

    private sealed class CacheEntry
    {
        public required ParserOptions ParserOptions { get; init; }
        public required Script Script { get; init; }
        public required JintScript JintScript { get; init; }
        public required bool ContainsArguments { get; init; }
        public required bool ContainsNewTarget { get; init; }
        public required bool ContainsSuperCall { get; init; }
        public required bool ContainsSuperProperty { get; init; }
    }

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

        var parserOptions = _engine.GetActiveParserOptions();
        ParserOptions adjustedParserOptions;
        if (ReferenceEquals(parserOptions, _lastBaseParserOptions))
        {
            adjustedParserOptions = _lastAdjustedParserOptions!;
        }
        else
        {
            adjustedParserOptions = parserOptions with
            {
                AllowReturnOutsideFunction = false,
                AllowNewTargetOutsideFunction = true,
                AllowSuperOutsideMethod = true,
                // This is a workaround, just makes some tests pass. Actually, we need these checks (done either by the parser or by the runtime).
                // TODO: implement a correct solution
                CheckPrivateFields = false
            };
            _lastBaseParserOptions = parserOptions;
            _lastAdjustedParserOptions = adjustedParserOptions;
        }

        // For indirect eval, parse in non-strict mode (strictness only from "use strict" in code)
        // For direct eval, inherit caller's strictness
        var strictParse = direct && strictCaller;
        var source = x.ToString();

        var cacheable = source.Length <= CacheMaxSourceLength;
        var cacheKey = new CacheKey(source, strictParse);
        if (!cacheable
            || _evalCache is null
            || !_evalCache.TryGetValue(cacheKey, out var cached)
            || !(ReferenceEquals(cached.ParserOptions, adjustedParserOptions) || cached.ParserOptions.Equals(adjustedParserOptions)))
        {
            var parser = _engine.GetParserFor(adjustedParserOptions);
            var parsedScript = parser.ParseScriptGuarded(_engine.Realm, source, strict: strictParse);

            var analyzer = new EvalScriptAnalyzer();
            analyzer.Visit(parsedScript);

            cached = new CacheEntry
            {
                ParserOptions = adjustedParserOptions,
                Script = parsedScript,
                JintScript = new JintScript(parsedScript),
                ContainsArguments = analyzer._containsArguments,
                ContainsNewTarget = analyzer._containsNewTarget,
                ContainsSuperCall = analyzer._containsSuperCall,
                ContainsSuperProperty = analyzer._containsSuperProperty,
            };

            if (cacheable)
            {
                // Promote into the cache only on the second sighting of the same source.
                if (cacheKey.Equals(_probationKey))
                {
                    var cache = _evalCache ??= new Dictionary<CacheKey, CacheEntry>();
                    if (cache.Count >= CacheCapacity)
                    {
                        cache.Clear();
                    }
                    cache[cacheKey] = cached;
                }
                else
                {
                    _probationKey = cacheKey;
                }
            }
        }

        var script = cached.Script;
        var body = script.Body;
        if (body.Count == 0)
        {
            return Undefined;
        }

        if (!inFunction)
        {
            // if body Contains NewTarget, throw a SyntaxError exception.
            if (cached.ContainsNewTarget)
            {
                Throw.SyntaxError(evalRealm, "new.target expression is not allowed here");
            }
        }

        if (!inMethod)
        {
            // if body Contains SuperProperty, throw a SyntaxError exception.
            if (cached.ContainsSuperProperty)
            {
                Throw.SyntaxError(evalRealm, "'super' keyword unexpected here");
            }
        }

        if (!inDerivedConstructor)
        {
            // if body Contains SuperCall, throw a SyntaxError exception.
            if (cached.ContainsSuperCall)
            {
                Throw.SyntaxError(evalRealm, "'super' keyword unexpected here");
            }
        }

        if (inClassFieldInitializer)
        {
            // if ContainsArguments of body is true, throw a SyntaxError exception.
            if (cached.ContainsArguments)
            {
                Throw.SyntaxError(evalRealm, "'arguments' is not allowed in class field initializer or static initialization block");
            }
        }

        // Per ECMAScript 19.2.1.1 step 6-7:
        // strictEval is true if:
        // - The eval code has a "use strict" directive, OR
        // - It's a DIRECT eval and the caller is in strict mode
        var strictEval = script.Strict || (direct && _engine._isStrict);
        var ctx = _engine.ExecutionContext;

        // For indirect eval, we need to force reset the strict mode scope
        // because the caller's strict mode should not apply
        using (new StrictModeScope(strictEval, force: !direct))
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

                var statement = cached.JintScript;
                var context = _engine._activeEvaluationContext ?? new EvaluationContext(_engine);
                var result = statement.Execute(context);

                var value = result.GetValueOrDefault();

                if (result.Type == CompletionType.Throw)
                {
                    Throw.JavaScriptException(_engine, value, result);
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

        protected override object? VisitFunctionDeclaration(FunctionDeclaration node)
        {
            return node;
        }

        protected override object? VisitFunctionExpression(FunctionExpression node)
        {
            return node;
        }
    }
}
