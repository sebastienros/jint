using System.Diagnostics.CodeAnalysis;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Statements;
using Jint.Runtime.Modules;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Native.ShadowRealm;

/// <summary>
/// https://tc39.es/proposal-shadowrealm/#sec-properties-of-shadowrealm-instances
/// </summary>
#pragma warning disable MA0049
public sealed class ShadowRealm : ObjectInstance
#pragma warning restore MA0049
{
    internal readonly Realm _shadowRealm;
    private readonly ExecutionContext _executionContext;

    internal ShadowRealm(Engine engine, in ExecutionContext executionContext, Realm shadowRealm) : base(engine)
    {
        _executionContext = executionContext;
        _shadowRealm = shadowRealm;
    }

    public JsValue Evaluate(string sourceText, ScriptParsingOptions? parsingOptions = null)
    {
        using var ownership = _engine.EnterHostCall();
        var callerRealm = _engine.Realm;
        var parser = parsingOptions is null
            ? _engine.GetParserFor(_engine.GetActiveParserOptions())
            : _engine.GetParserFor(parsingOptions);
        return PerformShadowRealmEval(sourceText, parser.Options, parser, callerRealm);
    }

    public JsValue Evaluate(in Prepared<Script> preparedScript)
    {
        using var ownership = _engine.EnterHostCall();
        if (!preparedScript.IsValid)
        {
            Throw.InvalidPreparedScriptArgumentException(nameof(preparedScript));
        }

        var callerRealm = _engine.Realm;
        return PerformShadowRealmEval(in preparedScript, callerRealm);
    }

    public JsValue ImportValue(string specifier, string exportName)
    {
        var callerRealm = _engine.Realm;
        var value = ShadowRealmImportValue(specifier, exportName, callerRealm);
        _engine.RunAvailableContinuations();
        return value;
    }

    /// <summary>
    /// Registers a delegate with given name on this shadow realm's global object. Delegate becomes a
    /// JavaScript function that can be called.
    /// </summary>
    /// <remarks>
    /// <b>This is the one <c>SetValue</c> overload whose property attributes differ</b>, exactly as on
    /// <see cref="Engine.SetValue(string, Delegate)"/>: a delegate is installed with
    /// <see cref="PropertyFlag.NonEnumerable"/> — writable and configurable, but <em>not</em> enumerable —
    /// while every other overload produces the ordinary configurable/enumerable/writable data property. So
    /// a delegate registered here does not appear in <c>Object.keys(globalThis)</c> or a <c>for..in</c>
    /// over the shadow realm's global object, and every other value does.
    /// </remarks>
    public ShadowRealm SetValue(string name, Delegate value)
    {
        using var ownership = _engine.EnterHostCall();
        GlobalValueRegistration.RegisterDelegate(_engine, _shadowRealm.GlobalObject, name, value);
        return this;
    }

    /// <summary>
    /// Registers a string value as variable on this shadow realm's global object.
    /// </summary>
    public ShadowRealm SetValue(string name, string? value)
    {
        return SetValue(name, value is null ? JsValue.Null : JsString.Create(value));
    }

    /// <summary>
    /// Registers a double value as variable on this shadow realm's global object.
    /// </summary>
    public ShadowRealm SetValue(string name, double value)
    {
        return SetValue(name, (JsValue) JsNumber.Create(value));
    }

    /// <summary>
    /// Registers an integer value as variable on this shadow realm's global object.
    /// </summary>
    public ShadowRealm SetValue(string name, int value)
    {
        return SetValue(name, (JsValue) JsNumber.Create(value));
    }

    /// <summary>
    /// Registers a boolean value as variable on this shadow realm's global object.
    /// </summary>
    public ShadowRealm SetValue(string name, bool value)
    {
        return SetValue(name, (JsValue) (value ? JsBoolean.True : JsBoolean.False));
    }

    /// <summary>
    /// Registers a native JS value as variable on this shadow realm's global object.
    /// </summary>
    public ShadowRealm SetValue(string name, JsValue value)
    {
        using var ownership = _engine.EnterHostCall();
        GlobalValueRegistration.Register(_shadowRealm.GlobalObject, name, value);
        return this;
    }

    /// <summary>
    /// Registers an object value as variable on this shadow realm's global object, creates an interop
    /// wrapper when needed.
    /// </summary>
    /// <remarks>
    /// This overload binds only where the argument's static type is <see cref="object"/> — a typed
    /// variable picks <see cref="SetValue{T}(string, T)"/>, whose type parameter carries
    /// <c>[DynamicallyAccessedMembers]</c> and therefore preserves what Jint reflects over. Here there is
    /// no type to annotate, which is the whole of the difference.
    /// </remarks>
    [RequiresUnreferencedCode(GlobalValueRegistration.RequiresUnreferencedCodeMessage)]
    public ShadowRealm SetValue(string name, object? obj)
    {
        using var ownership = _engine.EnterHostCall();
        GlobalValueRegistration.RegisterObject(_engine, _shadowRealm.GlobalObject, name, obj);
        return this;
    }

    /// <summary>
    /// Registers a CLR type as variable on this shadow realm's global object, creates an interop wrapper
    /// when needed.
    /// </summary>
    public ShadowRealm SetValue(string name, [DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] Type type)
    {
        using var ownership = _engine.EnterHostCall();
        GlobalValueRegistration.RegisterType(_engine, _shadowRealm.GlobalObject, name, type);
        return this;
    }

    /// <summary>
    /// Registers an object value as variable on this shadow realm's global object, creates an interop
    /// wrapper when needed.
    /// </summary>
    public ShadowRealm SetValue<[DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] T>(string name, T? obj)
    {
        using var ownership = _engine.EnterHostCall();
        GlobalValueRegistration.RegisterTyped(_engine, _shadowRealm.GlobalObject, name, obj);
        return this;
    }

    /// <summary>
    /// Registers an array as variable on this shadow realm's global object, creates an interop wrapper
    /// when needed. Behaves exactly like <see cref="SetValue{T}(string, T)"/>; it exists so that the
    /// annotation lands on the <em>element</em> type, for the reasons
    /// <see cref="Engine.SetValue{T}(string, T[])"/> spells out.
    /// </summary>
    public ShadowRealm SetValue<[DynamicallyAccessedMembers(InteropHelper.DefaultDynamicallyAccessedMemberTypes)] T>(string name, T[]? obj)
    {
        using var ownership = _engine.EnterHostCall();
        GlobalValueRegistration.RegisterArray(_engine, _shadowRealm.GlobalObject, name, obj);
        return this;
    }


    /// <summary>
    /// https://tc39.es/proposal-shadowrealm/#sec-performshadowrealmeval
    /// </summary>
    internal JsValue PerformShadowRealmEval(string sourceText, ParserOptions parserOptions, JintParser parser, Realm callerRealm)
    {
        var evalRealm = _shadowRealm;

        _engine._host.EnsureCanCompileStrings(callerRealm, evalRealm);

        Script script;
        try
        {
            script = parser.ParseScriptGuarded(callerRealm, sourceText, strict: _engine._isStrict);
        }
        catch (ParseErrorException e)
        {
            if (string.Equals(e.Error.Code, "InvalidLhsInAssignment", StringComparison.Ordinal))
            {
                Throw.ReferenceError(callerRealm, e.Description);
            }
            else
            {
                Throw.SyntaxError(callerRealm, e.Message);
            }

            return default;
        }

        return PerformShadowRealmEvalInternal(new Prepared<Script>(
            script,
            parserOptions,
            parsingConstraints: parser.Constraints), callerRealm);
    }

    internal JsValue PerformShadowRealmEval(in Prepared<Script> preparedScript, Realm callerRealm)
    {
        var evalRealm = _shadowRealm;

        _engine._host.EnsureCanCompileStrings(callerRealm, evalRealm);

        return PerformShadowRealmEvalInternal(in preparedScript, callerRealm);
    }

    internal JsValue PerformShadowRealmEvalInternal(in Prepared<Script> preparedScript, Realm callerRealm)
    {
        var evalRealm = _shadowRealm;

        var script = preparedScript.Program!;
        ref readonly var body = ref script.Body;
        if (body.Count == 0)
        {
            return Undefined;
        }

        var validator = new ShadowScriptValidator(callerRealm);
        validator.Visit(script);

        var strictEval = script.Strict;
        var runningContext = _engine.ExecutionContext;
        var lexEnv = JintEnvironment.NewDeclarativeEnvironment(_engine, evalRealm.GlobalEnv);
        Environment varEnv = evalRealm.GlobalEnv;

        if (strictEval)
        {
            varEnv = lexEnv;
        }

        // If runningContext is not already suspended, suspend runningContext.

        var parsingConstraints = _engine.CombineParsingConstraints(preparedScript.ParsingConstraints);
        var scriptRecord = new ScriptRecord(
            evalRealm,
            script,
            script.Location.SourceFile,
            parsingConstraints,
            preparedScript.ParserOptions);
        var evalContext = new ExecutionContext(
            scriptRecord,
            lexEnv,
            varEnv,
            null,
            evalRealm,
            null,
            parserOptions: preparedScript.ParserOptions,
            strict: strictEval);
        _engine.EnterExecutionContext(in evalContext);

        Completion result;
        try
        {
            _engine.EvalDeclarationInstantiation(script, script.GetHoistingScope(), varEnv, lexEnv, privateEnv: null, strictEval);

            // Joins the engine's context rather than substituting a fresh one. The old code did the
            // latter, with a TODO asking which was correct: a fresh context differed only in the
            // completion-observability flag, which the shadow script's own statement list sets on
            // entry and restores in a finally. The shadow realm itself arrives through the execution
            // context pushed above, which the evaluation context knows nothing about.
            result = new JintScript(script).Execute(_engine._evaluationContext);

            if (result.Type == CompletionType.Throw)
            {
                ThrowCrossRealmError(callerRealm, result.GetValueOrDefault().ToString());
            }
        }
        finally
        {
            _engine.LeaveExecutionContext();
        }

        return GetWrappedValue(callerRealm, callerRealm, result.Value);
    }


    /// <summary>
    /// https://tc39.es/proposal-shadowrealm/#sec-getwrappedvalue
    /// </summary>
    private static JsValue GetWrappedValue(Realm throwerRealm, Realm callerRealm, JsValue value)
    {
        if (value is ObjectInstance oi)
        {
            if (!oi.IsCallable)
            {
                ThrowCrossRealmError(throwerRealm, "Result is not callable");
            }

            return WrappedFunctionCreate(throwerRealm, callerRealm, oi);
        }

        return value;
    }

    /// <summary>
    /// https://tc39.es/proposal-shadowrealm/#sec-wrappedfunctioncreate
    /// </summary>
    private static WrappedFunction WrappedFunctionCreate(Realm throwerRealm, Realm callerRealm, ObjectInstance target)
    {
        var wrapped = new WrappedFunction(callerRealm.GlobalEnv._engine, callerRealm, target);
        try
        {
            CopyNameAndLength(wrapped, target);
        }
        catch (JavaScriptException ex)
        {
            ThrowCrossRealmError(throwerRealm, ex.Message);
        }

        return wrapped;
    }

    /// <summary>
    /// https://tc39.es/proposal-shadowrealm/#sec-copynameandlength
    /// </summary>
    private static void CopyNameAndLength(WrappedFunction f, ObjectInstance target, string? prefix = null, int argCount = 0)
    {
        var L = JsNumber.PositiveZero;
        var targetHasLength = target.HasOwnProperty("length");
        if (targetHasLength)
        {
            var targetLen = target.Get("length");
            if (targetLen is JsNumber number)
            {
                if (number.IsPositiveInfinity())
                {
                    L = number;
                }
                else if (number.IsNegativeInfinity())
                {
                    L = JsNumber.PositiveZero;
                }
                else
                {
                    var targetLenAsInt = TypeConverter.ToIntegerOrInfinity(targetLen);
                    L = JsNumber.Create(System.Math.Max(targetLenAsInt - argCount, 0));
                }
            }
        }

        f.SetFunctionLength(L);
        var targetName = target.Get(CommonProperties.Name);
        if (!targetName.IsString())
        {
            targetName = JsString.Empty;
        }

        f.SetFunctionName(targetName, prefix);
    }

    /// <summary>
    /// https://tc39.es/proposal-shadowrealm/#sec-shadowrealmimportvalue
    /// </summary>
    internal JsValue ShadowRealmImportValue(
        string specifierString,
        string exportNameString,
        Realm callerRealm)
    {
        var innerCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);

        // var runningContext = _engine.ExecutionContext;
        // 4. If runningContext is not already suspended, suspend runningContext.

        _engine.EnterExecutionContext(in _executionContext);
        try
        {
            var moduleRequest = new ModuleRequest(specifierString, []);
            _engine._host.LoadImportedModule(null, moduleRequest, new DynamicImportPayload(_engine, moduleRequest, innerCapability));
        }
        finally
        {
            // module resolution failures throw ModuleResolutionException (not JavaScriptException);
            // the entered frame must not leak — the execution-context depth gates the
            // host-boundary constraint checks
            _engine.LeaveExecutionContext();
        }

        var onFulfilled = new StepsFunction(_engine, callerRealm, exportNameString);
        var promiseCapability = PromiseConstructor.NewPromiseCapability(_engine, _engine.Realm.Intrinsics.Promise);
        var value = PromiseOperations.PerformPromiseThen(_engine, (JsPromise) innerCapability.PromiseInstance, onFulfilled, callerRealm.Intrinsics.ThrowTypeError, promiseCapability);

        return value;
    }

    private sealed class StepsFunction : Function.Function
    {
        private readonly string _exportNameString;

        public StepsFunction(Engine engine, Realm realm, string exportNameString) : base(engine, realm, JsString.Empty)
        {
            _exportNameString = exportNameString;
            SetFunctionLength(JsNumber.PositiveOne);
        }

        protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
        {
            var exports = (ModuleNamespace) arguments.At(0);
            var f = this;
            var s = _exportNameString;
            var hasOwn = exports.HasOwnProperty(s);
            if (!hasOwn)
            {
                Throw.TypeError(_realm, $"export name {s} missing");
            }

            var value = exports.Get(s);
            var realm = f._realm;
            return GetWrappedValue(_engine.Realm, realm, value);
        }
    }

    private static ShadowRealm ValidateShadowRealmObject(Realm callerRealm, JsValue thisObj)
    {
        var instance = thisObj as ShadowRealm;
        if (instance is null)
        {
            Throw.TypeError(callerRealm, "object must be a ShadowRealm");
        }

        return instance;
    }

    private static void ThrowCrossRealmError(Realm callerRealm, string message)
    {
        Throw.TypeError(callerRealm, "Cross-Realm Error: " + message);
    }

    private sealed class WrappedFunction : Function.Function
    {
        private readonly ObjectInstance _wrappedTargetFunction;

        public WrappedFunction(
            Engine engine,
            Realm callerRealm,
            ObjectInstance wrappedTargetFunction) : base(engine, callerRealm, null)
        {
            _wrappedTargetFunction = wrappedTargetFunction;
            _prototype = callerRealm.Intrinsics.Function.PrototypeObject;
        }

        /// <summary>
        /// https://tc39.es/proposal-shadowrealm/#sec-wrapped-function-exotic-objects-call-thisargument-argumentslist
        /// </summary>
        protected internal override JsValue Call(JsValue thisArgument, JsCallArguments arguments)
        {
            var target = _wrappedTargetFunction;
            var targetRealm = GetFunctionRealm(target);
            var callerRealm = GetFunctionRealm(this);

            var wrappedArgs = new JsValue[arguments.Length];
            for (var i = 0; i < arguments.Length; i++)
            {
                wrappedArgs[i] = GetWrappedValue(callerRealm, targetRealm, arguments[i]);
            }

            var wrappedThisArgument = GetWrappedValue(callerRealm, targetRealm, thisArgument);

            JsValue result;
            try
            {
                result = target.Call(wrappedThisArgument, wrappedArgs);
            }
            catch (JavaScriptException ex)
            {
                ThrowCrossRealmError(_realm, ex.Message);
                return default!;
            }

            return GetWrappedValue(callerRealm, callerRealm, result);
        }

        public override string ToString() => _wrappedTargetFunction.ToString();
    }

    /// <summary>
    /// https://tc39.es/proposal-shadowrealm/#sec-performshadowrealmeval
    ///
    /// If body Contains NewTarget is true, throw a SyntaxError exception.
    /// If body Contains SuperProperty is true, throw a SyntaxError exception.
    /// If body Contains SuperCall is true, throw a SyntaxError exception.
    ///
    /// Mirrors the static semantics Contains (https://tc39.es/ecma262/#sec-static-semantics-contains):
    /// the search must not descend into nested function bodies or class element values, where
    /// super/new.target are legal, only into arrow functions (transparent for super/new.target),
    /// class heritage expressions, decorators and computed property names.
    /// </summary>
    private sealed class ShadowScriptValidator : AstVisitor
    {
        private readonly Realm _realm;

        public ShadowScriptValidator(Realm realm)
        {
            _realm = realm;
        }

        protected override object VisitSuper(Super super)
        {
            Throw.SyntaxError(_realm, "'super' keyword unexpected here");
            return super;
        }

        protected override object VisitMetaProperty(MetaProperty metaProperty)
        {
            // new.target; import.meta cannot occur in a script
            if (string.Equals(metaProperty.Meta.Name, "new", StringComparison.Ordinal))
            {
                Throw.SyntaxError(_realm, "new.target expression is not allowed here");
            }
            return metaProperty;
        }

        protected override object VisitFunctionDeclaration(FunctionDeclaration node) => node;

        protected override object VisitFunctionExpression(FunctionExpression node) => node;

        protected override object VisitMethodDefinition(MethodDefinition node) => VisitClassProperty(node, node.Decorators);

        protected override object VisitPropertyDefinition(PropertyDefinition node) => VisitClassProperty(node, node.Decorators);

        protected override object VisitAccessorProperty(AccessorProperty node) => VisitClassProperty(node, node.Decorators);

        protected override object VisitStaticBlock(StaticBlock node) => node;

        private ClassProperty VisitClassProperty(ClassProperty node, in NodeList<Decorator> decorators)
        {
            // decorators and a computed property name evaluate in the scope enclosing the class;
            // the value (method body or field initializer) has its own home object
            foreach (var decorator in decorators)
            {
                Visit(decorator);
            }

            if (node.Computed)
            {
                Visit(node.Key);
            }

            return node;
        }
    }
}
