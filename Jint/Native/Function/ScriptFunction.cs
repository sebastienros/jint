using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Native.Function;

public sealed class ScriptFunction : Function, IConstructor
{
    internal bool _isClassConstructor;
    internal JsValue? _classFieldInitializerName;

    internal List<PrivateElement>? _privateMethods;
    internal List<ClassFieldDefinition>? _fields;

    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2
    /// </summary>
    public ScriptFunction(
        Engine engine,
        IFunction functionDeclaration,
        bool strict,
        ObjectInstance? proto = null)
        : this(
            engine,
            new JintFunctionDefinition(functionDeclaration),
            JintEnvironment.NewDeclarativeEnvironment(engine, engine.ExecutionContext.LexicalEnvironment),
            strict ? FunctionThisMode.Strict : FunctionThisMode.Global,
            proto)
    {
    }

    internal ScriptFunction(
        Engine engine,
        JintFunctionDefinition function,
        Environment env,
        FunctionThisMode thisMode,
        ObjectInstance? proto = null)
        : base(engine, engine.Realm, function, env, thisMode)
    {
        _prototype = proto ?? _engine.Realm.Intrinsics.Function.PrototypeObject;
        _length = new LazyPropertyDescriptor<JintFunctionDefinition>(function, static function => JsNumber.Create(function.Initialize().Length), PropertyFlag.Configurable);

        if (!function.Strict
            && function.Function is not ArrowFunctionExpression
            && !function.Function.Generator)
        {
            SetProperty(KnownKeys.Arguments, new GetSetPropertyDescriptor.ThrowerPropertyDescriptor(engine, PropertyFlag.Configurable));
            SetProperty(KnownKeys.Caller, new PropertyDescriptor(Undefined, PropertyFlag.Configurable));
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ecmascript-function-objects-call-thisargument-argumentslist
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        var strict = _functionDefinition!.Strict || _thisMode == FunctionThisMode.Strict;
        using (new StrictModeScope(strict, true))
        {
            try
            {
                ref readonly var calleeContext = ref PrepareForOrdinaryCall(Undefined);

                if (_isClassConstructor)
                {
                    ExceptionHelper.ThrowTypeError(calleeContext.Realm, $"Class constructor {_functionDefinition.Name} cannot be invoked without 'new'");
                }

                OrdinaryCallBindThis(calleeContext, thisObject);

                // actual call
                var context = _engine._activeEvaluationContext ?? new EvaluationContext(_engine);

                var result = _functionDefinition.EvaluateBody(context, this, arguments);

                if (result.Type == CompletionType.Throw)
                {
                    ExceptionHelper.ThrowJavaScriptException(_engine, result.Value, result);
                }

                // The DebugHandler needs the current execution context before the return for stepping through the return point
                if (context.DebugMode)
                {
                    // We don't have a statement, but we still need a Location for debuggers. DebugHandler will infer one from
                    // the function body:
                    _engine.Debugger.OnReturnPoint(
                        _functionDefinition.Function.Body,
                        result.Type == CompletionType.Normal ? Undefined : result.Value
                    );
                }

                if (result.Type == CompletionType.Return)
                {
                    return result.Value;
                }
            }
            finally
            {
                _engine.LeaveExecutionContext();
            }

            return Undefined;
        }
    }

    internal override bool IsConstructor
    {
        get
        {
            if (!_homeObject.IsUndefined() && !_isClassConstructor)
            {
                return false;
            }

            var function = _functionDefinition?.Function;
            return function is not null
                   && function is not ArrowFunctionExpression
                   && !function.Generator
                   && !function.Async;
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ecmascript-function-objects-construct-argumentslist-newtarget
    /// </summary>
    ObjectInstance IConstructor.Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var callerContext = _engine.ExecutionContext;
        var kind = _constructorKind;

        var thisArgument = Undefined;

        if (kind == ConstructorKind.Base)
        {
            thisArgument = OrdinaryCreateFromConstructor(
                newTarget,
                static intrinsics => intrinsics.Object.PrototypeObject,
                static (Engine engine, Realm _, object? _) => new JsObject(engine));
        }

        ref readonly var calleeContext = ref PrepareForOrdinaryCall(newTarget);
        var constructorEnv = (FunctionEnvironment) calleeContext.LexicalEnvironment;

        var strict = _thisMode == FunctionThisMode.Strict;
        using (new StrictModeScope(strict, force: true))
        {
            try
            {
                if (kind == ConstructorKind.Base)
                {
                    OrdinaryCallBindThis(calleeContext, thisArgument);
                    ((ObjectInstance) thisArgument).InitializeInstanceElements(this);
                }

                var context = _engine._activeEvaluationContext ?? new EvaluationContext(_engine);

                var result = _functionDefinition!.EvaluateBody(context, this, arguments);

                // The DebugHandler needs the current execution context before the return for stepping through the return point
                // We exclude the empty constructor generated for classes without an explicit constructor.
                bool isStep = context.DebugMode &&
                              result.Type != CompletionType.Throw &&
                              _functionDefinition.Function != ClassDefinition._emptyConstructor.Value;
                if (isStep)
                {
                    // We don't have a statement, but we still need a Location for debuggers. DebugHandler will infer one from
                    // the function body:
                    _engine.Debugger.OnReturnPoint(
                        _functionDefinition.Function.Body,
                        result.Type == CompletionType.Normal ? thisArgument : result.Value
                    );
                }

                if (result.Type == CompletionType.Return)
                {
                    if (result.Value is ObjectInstance oi)
                    {
                        return oi;
                    }

                    if (kind == ConstructorKind.Base)
                    {
                        return (ObjectInstance) thisArgument;
                    }

                    if (!result.Value.IsUndefined())
                    {
                        ExceptionHelper.ThrowTypeError(callerContext.Realm);
                    }
                }
                else if (result.Type == CompletionType.Throw)
                {
                    ExceptionHelper.ThrowJavaScriptException(_engine, result.Value, result);
                }
            }
            finally
            {
                _engine.LeaveExecutionContext();
            }
        }

        return (ObjectInstance) constructorEnv.GetThisBinding();
    }

    internal void MakeClassConstructor()
    {
        _isClassConstructor = true;
    }
}
