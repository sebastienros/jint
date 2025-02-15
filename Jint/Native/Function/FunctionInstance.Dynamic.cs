using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Native.Function;

#pragma warning disable MA0049
public partial class Function
#pragma warning restore MA0049
{
    private static readonly JsString _functionNameAnonymous = new JsString("anonymous");

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createdynamicfunction
    /// </summary>
    internal Function CreateDynamicFunction(
        ObjectInstance constructor,
        JsValue newTarget,
        FunctionKind kind,
        JsCallArguments arguments)
    {
        // TODO var callerContext = _engine.GetExecutionContext(1);
        var callerContext = _engine.ExecutionContext;
        var callerRealm = callerContext.Realm;
        var calleeRealm = _engine.ExecutionContext.Realm;

        _engine._host.EnsureCanCompileStrings(callerRealm, calleeRealm);

        if (newTarget.IsUndefined())
        {
            newTarget = constructor;
        }

        Func<Intrinsics, ObjectInstance>? fallbackProto = null;
        switch (kind)
        {
            case FunctionKind.Normal:
                fallbackProto = static intrinsics => intrinsics.Function.PrototypeObject;
                break;
            case FunctionKind.Async:
                fallbackProto = static intrinsics => intrinsics.AsyncFunction.PrototypeObject;
                break;
            case FunctionKind.Generator:
                fallbackProto = static intrinsics => intrinsics.GeneratorFunction.PrototypeObject;
                break;
            case FunctionKind.AsyncGenerator:
                fallbackProto = static intrinsics => intrinsics.AsyncGeneratorFunction.PrototypeObject;
                break;
            default:
                ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(kind), kind.ToString());
                break;
        }

        var argCount = arguments.Length;
        var p = "";
        var body = "";

        if (argCount == 1)
        {
            body = TypeConverter.ToString(arguments[0]);
        }
        else if (argCount > 1)
        {
            var firstArg = arguments[0];
            p = TypeConverter.ToString(firstArg);
            for (var k = 1; k < argCount - 1; k++)
            {
                var nextArg = arguments[k];
                p += "," + TypeConverter.ToString(nextArg);
            }

            body = TypeConverter.ToString(arguments[argCount - 1]);
        }

        IFunction? function = null;
        try
        {
            string? functionExpression = null;
            if (argCount == 0)
            {
                switch (kind)
                {
                    case FunctionKind.Normal:
                        functionExpression = "function f(){}";
                        break;
                    case FunctionKind.Generator:
                        functionExpression = "function* f(){}";
                        break;
                    case FunctionKind.Async:
                        functionExpression = "async function f(){}";
                        break;
                    case FunctionKind.AsyncGenerator:
                        functionExpression = "async function* f(){}";
                        break;
                    default:
                        ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(kind), kind.ToString());
                        break;
                }
            }
            else
            {
                switch (kind)
                {
                    case FunctionKind.Normal:
                        functionExpression = "function f(";
                        break;
                    case FunctionKind.Async:
                        functionExpression = "async function f(";
                        break;
                    case FunctionKind.Generator:
                        functionExpression = "function* f(";
                        break;
                    case FunctionKind.AsyncGenerator:
                        functionExpression = "async function* f(";
                        break;
                    default:
                        ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(kind), kind.ToString());
                        break;
                }

                if (p.Contains('/'))
                {
                    // ensure comments don't screw up things
                    functionExpression += "\n" + p + "\n";
                }
                else
                {
                    functionExpression += p;
                }

                functionExpression += ")";

                if (body.Contains('/'))
                {
                    // ensure comments don't screw up things
                    functionExpression += "{\n" + body + "\n}";
                }
                else
                {
                    functionExpression += "{" + body + "}";
                }
            }

            var parserOptions = _engine.GetActiveParserOptions();
            if (!parserOptions.AllowReturnOutsideFunction)
            {
                parserOptions = parserOptions with { AllowReturnOutsideFunction = true };
            }
            Parser parser = new(parserOptions);
            function = (IFunction) parser.ParseScriptGuarded(callerRealm, functionExpression, strict: _engine._isStrict).Body[0];
        }
        catch (ParseErrorException ex)
        {
            ExceptionHelper.ThrowSyntaxError(_engine.ExecutionContext.Realm, ex.Message);
        }

        var proto = GetPrototypeFromConstructor(newTarget, fallbackProto);
        var realmF = _realm;
        var scope = realmF.GlobalEnv;
        PrivateEnvironment? privateEnv = null;

        var definition = new JintFunctionDefinition(function);
        Function F = OrdinaryFunctionCreate(proto, definition, function.IsStrict() ? FunctionThisMode.Strict : FunctionThisMode.Global, scope, privateEnv);
        F.SetFunctionName(_functionNameAnonymous, force: true);

        if (kind == FunctionKind.Generator)
        {
            var prototype = OrdinaryObjectCreate(_engine, _realm.Intrinsics.GeneratorFunction.PrototypeObject.PrototypeObject);
            F.DefinePropertyOrThrow(CommonProperties.Prototype, new PropertyDescriptor(prototype, PropertyFlag.Writable));
        }
        else if (kind == FunctionKind.AsyncGenerator)
        {
            var prototype = OrdinaryObjectCreate(_engine, _realm.Intrinsics.AsyncGeneratorFunction.PrototypeObject.PrototypeObject);
            F.DefinePropertyOrThrow(CommonProperties.Prototype, new PropertyDescriptor(prototype, PropertyFlag.Writable));
        }
        else if (kind == FunctionKind.Normal)
        {
            F.MakeConstructor();
        }

        return F;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinaryfunctioncreate
    /// </summary>
    internal ScriptFunction OrdinaryFunctionCreate(
        ObjectInstance functionPrototype,
        JintFunctionDefinition function,
        FunctionThisMode thisMode,
        Environment scope,
        PrivateEnvironment? privateScope)
    {
        return new ScriptFunction(
            _engine,
            function,
            scope,
            thisMode,
            functionPrototype) { _privateEnvironment = privateScope, _realm = _realm };
    }
}
