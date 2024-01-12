using Esprima;
using Esprima.Ast;
using Esprima.Utils;
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
    private readonly JavaScriptParser _parser;
    internal readonly Realm _shadowRealm;
    private readonly ExecutionContext _executionContext;

    internal ShadowRealm(Engine engine, ExecutionContext executionContext, Realm shadowRealm) : base(engine)
    {
        _parser = new(new ParserOptions
        {
            Tolerant = false,
            RegexTimeout = engine.Options.Constraints.RegexTimeout
        });
        _executionContext = executionContext;
        _shadowRealm = shadowRealm;
    }

    public JsValue Evaluate(string sourceText)
    {
        var callerRealm = _engine.Realm;
        return PerformShadowRealmEval(sourceText, callerRealm);
    }

    public JsValue Evaluate(Script script)
    {
        var callerRealm = _engine.Realm;
        return PerformShadowRealmEval(script, callerRealm);
    }

    public JsValue ImportValue(string specifier, string exportName)
    {
        var callerRealm = _engine.Realm;
        var value = ShadowRealmImportValue(specifier, exportName, callerRealm);
        _engine.RunAvailableContinuations();
        return value;
    }
    public ShadowRealm SetValue(string name, Delegate value)
    {
        _shadowRealm.GlobalObject.FastSetProperty(name, new PropertyDescriptor(new DelegateWrapper(_engine, value), true, false, true));
        return this;
    }

    public ShadowRealm SetValue(string name, string value)
    {
        return SetValue(name, JsString.Create(value));
    }

    public ShadowRealm SetValue(string name, double value)
    {
        return SetValue(name, JsNumber.Create(value));
    }

    public ShadowRealm SetValue(string name, int value)
    {
        return SetValue(name, JsNumber.Create(value));
    }

    public ShadowRealm SetValue(string name, bool value)
    {
        return SetValue(name, value ? JsBoolean.True : JsBoolean.False);
    }

    public ShadowRealm SetValue(string name, JsValue value)
    {
        _shadowRealm.GlobalObject.Set(name, value);
        return this;
    }

    public ShadowRealm SetValue(string name, object obj)
    {
        var value = obj is Type t
            ? TypeReference.CreateTypeReference(_engine, t)
            : JsValue.FromObject(_engine, obj);

        return SetValue(name, value);
    }


    /// <summary>
    /// https://tc39.es/proposal-shadowrealm/#sec-performshadowrealmeval
    /// </summary>
    internal JsValue PerformShadowRealmEval(string sourceText, Realm callerRealm)
    {
        var evalRealm = _shadowRealm;

        _engine._host.EnsureCanCompileStrings(callerRealm, evalRealm);

        Script script;
        try
        {
            script = _parser.ParseScript(sourceText, source: null, _engine._isStrict);
        }
        catch (ParserException e)
        {
            if (string.Equals(e.Description, Messages.InvalidLHSInAssignment, StringComparison.Ordinal))
            {
                ExceptionHelper.ThrowReferenceError(callerRealm, Messages.InvalidLHSInAssignment);
            }
            else
            {
                ExceptionHelper.ThrowSyntaxError(callerRealm, e.Message);
            }

            return default;
        }

        return PerformShadowRealmEvalInternal(script, callerRealm);
    }

    internal JsValue PerformShadowRealmEval(Script script, Realm callerRealm)
    {
        var evalRealm = _shadowRealm;

        _engine._host.EnsureCanCompileStrings(callerRealm, evalRealm);

        return PerformShadowRealmEvalInternal(script, callerRealm);
    }

    internal JsValue PerformShadowRealmEvalInternal(Script script, Realm callerRealm)
    {
        var evalRealm = _shadowRealm;

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

        var evalContext = new ExecutionContext(null, lexEnv, varEnv, null, evalRealm, null);
        _engine.EnterExecutionContext(evalContext);

        Completion result;
        try
        {
            _engine.EvalDeclarationInstantiation(script, varEnv, lexEnv, privateEnv: null, strictEval);

            using (new StrictModeScope(strictEval, force: true))
            {
                result = new JintScript(script).Execute(new EvaluationContext(_engine));
            }

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

        _engine.EnterExecutionContext(_executionContext);
        _engine._host.LoadImportedModule(null, new ModuleRequest(specifierString, []), innerCapability);
        _engine.LeaveExecutionContext();

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

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            var exports = (ModuleNamespace) arguments.At(0);
            var f = this;
            var s = _exportNameString;
            var hasOwn = exports.HasOwnProperty(s);
            if (!hasOwn)
            {
                ExceptionHelper.ThrowTypeError(_realm, $"export name {s} missing");
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
            ExceptionHelper.ThrowTypeError(callerRealm, "object must be a ShadowRealm");
        }

        return instance;
    }

    private static void ThrowCrossRealmError(Realm callerRealm, string message)
    {
        ExceptionHelper.ThrowTypeError(callerRealm, "Cross-Realm Error: " + message);
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
        protected internal override JsValue Call(JsValue thisArgument, JsValue[] arguments)
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
    }

    /// <summary>
    /// If body Contains NewTarget is true, throw a SyntaxError exception.
    /// If body Contains SuperProperty is true, throw a SyntaxError exception.
    /// If body Contains SuperCall is true, throw a SyntaxError exception.
    /// </summary>
    private sealed class ShadowScriptValidator : AstVisitor
    {
        private readonly Realm _realm;

        public ShadowScriptValidator(Realm realm)
        {
            _realm = realm;
        }

        protected override object? VisitSuper(Super super)
        {
            ExceptionHelper.ThrowTypeError(_realm, "Shadow realm code cannot contain super");
            return null;
        }
    }
}
