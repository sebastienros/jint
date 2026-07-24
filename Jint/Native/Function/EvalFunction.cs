using System.Threading;
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
    // validated on hit. Entries hold the parsed AST, the prebuilt statement tree, the hoisting
    // info and the static analysis flags. Parse failures are never cached.
    private const int CacheCapacity = 32;
    private const int CacheMaxSourceLength = 32 * 1024;

    // Strict-eval environments with at most this many bindings are backed by fixed slots
    // (same storage the function/block environments use) instead of a dictionary, enabling
    // the per-AST-node slot caches inside the eval body. Mirrors the block-scope cap.
    private const int MaxEvalSlots = 16;

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
        public required HoistingScope HoistingScope { get; init; }
        public required bool ContainsArguments { get; init; }
        public required bool ContainsNewTarget { get; init; }
        public required bool ContainsSuperCall { get; init; }
        public required bool ContainsSuperProperty { get; init; }

        // Fixed-slot layout for the strict-eval environment; null when ineligible (too many
        // bindings, none at all, or a body that cannot amortize the setup — no loop and never
        // promoted). Templates hold the post-instantiation state: vars and function names
        // initialized to undefined, let/const uninitialized (TDZ).
        public Key[]? SlotNames { get; set; }
        public Binding[]? SlotTemplates { get; set; }

        // Pooled environment reused across strict evals of this source when the body cannot
        // capture the environment (no closures, nested eval or with). The per-AST-node slot
        // caches in the shared JintScript stay valid only while the env instance is stable,
        // which is what this pooling provides for repeated eval on one engine.
        public DeclarativeEnvironment? _cachedEnv;

        // Lazily computed poolability: 0 = unknown, 1 = poolable, -1 = the body may capture
        // the environment.
        public sbyte _canPool;
    }

    internal EvalFunction(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype)
        : base(
            engine,
            realm,
            _functionName,
            engine.ExecutionContext.Strict ? FunctionThisMode.Strict : FunctionThisMode.Global)
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
        var entryIsShared = true;
        if (!cacheable
            || _evalCache is null
            || !_evalCache.TryGetValue(cacheKey, out var cached)
            || !(ReferenceEquals(cached.ParserOptions, adjustedParserOptions) || cached.ParserOptions.Equals(adjustedParserOptions)))
        {
            entryIsShared = false;

            var parser = _engine.GetParserFor(adjustedParserOptions);
            var parsedScript = parser.ParseScriptGuarded(_engine.Realm, source, strict: strictParse);

            var analyzer = new EvalScriptAnalyzer();
            analyzer.Visit(parsedScript);

            var hoistingScope = HoistingScope.GetProgramLevelDeclarations(parsedScript, collectVarNames: analyzer._containsLoop);

            // The fixed-slot machinery only pays off when it can amortize: a loop in the body
            // (many accesses within one call) or a promoted source (repeats, so the pooled
            // environment carries the per-node caches across calls). One-shot loopless evals
            // skip the layout cost entirely.
            Key[]? slotNames = null;
            Binding[]? slotTemplates = null;
            if (analyzer._containsLoop)
            {
                BuildEvalSlotLayout(hoistingScope, out slotNames, out slotTemplates);
            }

            cached = new CacheEntry
            {
                ParserOptions = adjustedParserOptions,
                Script = parsedScript,
                JintScript = new JintScript(parsedScript),
                HoistingScope = hoistingScope,
                SlotNames = slotNames,
                SlotTemplates = slotTemplates,
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
                    if (!analyzer._containsLoop)
                    {
                        BuildEvalSlotLayout(hoistingScope, out slotNames, out slotTemplates);
                        cached.SlotNames = slotNames;
                        cached.SlotTemplates = slotTemplates;
                    }

                    var cache = _evalCache ??= new Dictionary<CacheKey, CacheEntry>();
                    if (cache.Count >= CacheCapacity)
                    {
                        cache.Clear();
                    }
                    cache[cacheKey] = cached;
                    entryIsShared = true;
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

        // strictEval is established directly on the pushed eval execution context below. For
        // indirect eval a fresh context is pushed whose environments inherit nothing from the
        // caller, so the caller's strictness cannot leak in - this reproduces the former
        // StrictModeScope(strictEval, force: !direct) exactly.
        Environment outerLexEnv;
        Environment varEnv;
        PrivateEnvironment? privateEnv;
        if (direct)
        {
            outerLexEnv = ctx.LexicalEnvironment;
            varEnv = ctx.VariableEnvironment;
            privateEnv = ctx.PrivateEnvironment;
        }
        else
        {
            outerLexEnv = evalRealm.GlobalEnv;
            varEnv = evalRealm.GlobalEnv;
            privateEnv = null;
        }

        DeclarativeEnvironment lexEnv;
        var useSlots = strictEval && cached.SlotNames is not null;
        if (useSlots)
        {
            // Rent the pooled environment for this source, or build a fresh slot-backed one
            // from the cached templates. Bindings come up in their post-instantiation state
            // (a parked environment was reset to the templates when it was pooled).
            var pooled = Interlocked.Exchange(ref cached._cachedEnv, null);
            if (pooled is not null && ReferenceEquals(pooled._engine, _engine))
            {
                pooled._outerEnv = outerLexEnv;
                lexEnv = pooled;
            }
            else
            {
                lexEnv = JintEnvironment.NewDeclarativeEnvironment(_engine, outerLexEnv);
                lexEnv._slotNames = cached.SlotNames;
                lexEnv._slots = (Binding[]) cached.SlotTemplates!.Clone();
            }
            varEnv = lexEnv;
        }
        else
        {
            lexEnv = JintEnvironment.NewDeclarativeEnvironment(_engine, outerLexEnv);
            if (strictEval)
            {
                varEnv = lexEnv;
            }
        }

        // If ctx is not already suspended, suspend ctx.

        Engine.EnterExecutionContext(lexEnv, varEnv, evalRealm, privateEnv, strictEval);

        try
        {
            Engine.EvalDeclarationInstantiation(script, cached.HoistingScope, varEnv, lexEnv, privateEnv, strictEval, bindingsPreInitialized: useSlots);

            var statement = cached.JintScript;
            var context = _engine._activeEvaluationContext ?? new EvaluationContext(_engine);
            var result = statement.Execute(context);

            var value = result.GetValueOrDefault();

            if (result.Type == CompletionType.Throw)
            {
                Throw.JavaScriptException(_engine, value, result);
                return null!;
            }

            if (useSlots && entryIsShared)
            {
                TryPoolEvalEnvironment(cached, lexEnv, script);
            }

            return value;
        }
        finally
        {
            Engine.LeaveExecutionContext();
        }
    }

    /// <summary>
    /// Builds the fixed-slot layout for a strict-eval environment: the deduplicated union of
    /// var names, function-declaration names (both initialized to undefined, matching the state
    /// EvalDeclarationInstantiation would produce) and lexical names (uninitialized, TDZ).
    /// Yields null arrays when the body declares nothing or more than <see cref="MaxEvalSlots"/>
    /// unique names.
    /// </summary>
    private static void BuildEvalSlotLayout(HoistingScope hoistingScope, out Key[]? slotNames, out Binding[]? slotTemplates)
    {
        slotNames = null;
        slotTemplates = null;

        var names = new List<Key>();
        var templates = new List<Binding>();

        // Spec state after instantiation: CreateMutableBinding(name, canBeDeleted: true)
        // + InitializeBinding(undefined). Function bindings share the shape; the function
        // objects are written over the undefined values by the instantiation loop.
        var varTemplate = new Binding(JsValue.Undefined, canBeDeleted: true, mutable: true, strict: false);

        var varNames = hoistingScope._varNames;
        if (varNames is null && hoistingScope._variablesDeclarations != null)
        {
            // hoisting was computed without name collection (loopless body, layout deferred
            // to promotion) — gather from the declarations now
            varNames = new List<Key>();
            CachedHoistingScope.GatherVarNames(hoistingScope, varNames);
        }
        if (varNames != null)
        {
            foreach (var name in varNames)
            {
                if (!Contains(names, name))
                {
                    if (names.Count >= MaxEvalSlots)
                    {
                        return;
                    }
                    names.Add(name);
                    templates.Add(varTemplate);
                }
            }
        }

        var functionDeclarations = hoistingScope._functionDeclarations;
        if (functionDeclarations != null)
        {
            foreach (var d in functionDeclarations)
            {
                Key fn = d.Id!.Name;
                if (!Contains(names, fn))
                {
                    if (names.Count >= MaxEvalSlots)
                    {
                        return;
                    }
                    names.Add(fn);
                    templates.Add(varTemplate);
                }
            }
        }

        var lexicalDeclarations = hoistingScope._lexicalDeclarations;
        if (lexicalDeclarations != null)
        {
            var boundNames = new List<Key>();
            foreach (var d in lexicalDeclarations)
            {
                boundNames.Clear();
                d.GetBoundNames(boundNames);
                var template = d.IsConstantDeclaration()
                    ? new Binding(null!, canBeDeleted: false, mutable: false, strict: true)
                    : new Binding(null!, canBeDeleted: false, mutable: true, strict: false);
                foreach (var name in boundNames)
                {
                    if (!Contains(names, name))
                    {
                        if (names.Count >= MaxEvalSlots)
                        {
                            return;
                        }
                        names.Add(name);
                        templates.Add(template);
                    }
                }
            }
        }

        if (names.Count == 0)
        {
            return;
        }

        slotNames = names.ToArray();
        slotTemplates = templates.ToArray();

        static bool Contains(List<Key> list, Key name)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == name)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Offers the just-used strict-eval environment back to the cache entry so the next eval of
    /// the same source reuses the instance. Env identity stability is what keeps the shared
    /// statement tree's per-node slot caches valid across calls, so only bodies that cannot
    /// capture the environment (no closures, nested eval or with) are eligible.
    /// </summary>
    private static void TryPoolEvalEnvironment(CacheEntry cached, DeclarativeEnvironment env, Script script)
    {
        var canPool = cached._canPool;
        if (canPool == 0)
        {
            canPool = JintFunctionDefinition.EnvironmentEscapeAstVisitor.MayEscape(script) ? (sbyte) -1 : (sbyte) 1;
            cached._canPool = canPool;
        }

        if (canPool > 0)
        {
            // Don't root anything while parked in the cache: neither the caller's scope
            // chain nor the completed call's binding values (which could keep arbitrarily
            // large object graphs alive for the engine's lifetime). Resetting here also
            // means a rented environment needs no per-rent cleanup.
            env._outerEnv = null;
            env.Clear();
            env.ClearDisposeCapability();
            ResetSlots(env._slots!, cached.SlotTemplates!);
            Interlocked.Exchange(ref cached._cachedEnv, env);
        }
    }

    private static void ResetSlots(Binding[] slots, Binding[] templates)
    {
        templates.AsSpan().CopyTo(slots);
    }

    private sealed class EvalScriptAnalyzer : AstVisitor
    {
        public bool _containsArguments;
        public bool _containsNewTarget;
        public bool _containsSuperCall;
        public bool _containsSuperProperty;

        // Loops are what amortize the fixed-slot machinery within a single eval call; loops
        // inside nested functions run in their own environments and are deliberately not
        // visited (function visits below don't descend).
        public bool _containsLoop;

        protected override object VisitIdentifier(Identifier identifier)
        {
            _containsArguments |= string.Equals(identifier.Name, "arguments", StringComparison.Ordinal);
            return identifier;
        }

        protected override object? VisitArrowFunctionExpression(ArrowFunctionExpression arrowFunctionExpression)
        {
            // Arrows are transparent to arguments/new.target/super detection so they must be
            // visited, but their loops run in their own environment, not the eval body's.
            var containsLoop = _containsLoop;
            var result = base.VisitArrowFunctionExpression(arrowFunctionExpression);
            _containsLoop = containsLoop;
            return result;
        }

        protected override object? VisitForStatement(ForStatement forStatement)
        {
            _containsLoop = true;
            return base.VisitForStatement(forStatement);
        }

        protected override object? VisitWhileStatement(WhileStatement whileStatement)
        {
            _containsLoop = true;
            return base.VisitWhileStatement(whileStatement);
        }

        protected override object? VisitDoWhileStatement(DoWhileStatement doWhileStatement)
        {
            _containsLoop = true;
            return base.VisitDoWhileStatement(doWhileStatement);
        }

        protected override object? VisitForInStatement(ForInStatement forInStatement)
        {
            _containsLoop = true;
            return base.VisitForInStatement(forInStatement);
        }

        protected override object? VisitForOfStatement(ForOfStatement forOfStatement)
        {
            _containsLoop = true;
            return base.VisitForOfStatement(forOfStatement);
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
