using Esprima;
using Esprima.Ast;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Function;

public partial class FunctionInstance
{
    private static readonly JsString _functionNameAnonymous = new JsString("anonymous");

    /// <summary>
    /// https://tc39.es/ecma262/#sec-createdynamicfunction
    /// </summary>
    internal FunctionInstance CreateDynamicFunction(
        ObjectInstance constructor,
        JsValue newTarget,
        FunctionKind kind,
        JsValue[] arguments)
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
            case FunctionKind.AsyncGenerator:
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
                        ExceptionHelper.ThrowNotImplementedException("Async generators not implemented");
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
                        ExceptionHelper.ThrowNotImplementedException("Async generators not implemented");
                        break;
                    default:
                        ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(kind), kind.ToString());
                        break;
                }

                if (p.IndexOf('/') != -1)
                {
                    // ensure comments don't screw up things
                    functionExpression += "\n" + p + "\n";
                }
                else
                {
                    functionExpression += p;
                }

                functionExpression += ")";

                if (body.IndexOf('/') != -1)
                {
                    // ensure comments don't screw up things
                    functionExpression += "{\n" + body + "\n}";
                }
                else
                {
                    functionExpression += "{" + body + "}";
                }
            }

            JavaScriptParser parser = new(new ParserOptions
            {
                Tolerant = false,
                RegexTimeout = _engine.Options.Constraints.RegexTimeout
            });
            function = (IFunction) parser.ParseScript(functionExpression, source: null, _engine._isStrict).Body[0];
        }
        catch (ParserException ex)
        {
            ExceptionHelper.ThrowSyntaxError(_engine.ExecutionContext.Realm, ex.Message);
        }

        var proto = GetPrototypeFromConstructor(newTarget, fallbackProto);
        var realmF = _realm;
        var scope = realmF.GlobalEnv;
        PrivateEnvironmentRecord? privateEnv = null;

        var definition = new JintFunctionDefinition(function);
        FunctionInstance F = OrdinaryFunctionCreate(proto, definition, function.Strict ? FunctionThisMode.Strict : FunctionThisMode.Global, scope, privateEnv);
        F.SetFunctionName(_functionNameAnonymous, force: true);

        if (kind == FunctionKind.Generator)
        {
            ExceptionHelper.ThrowNotImplementedException("generators not implemented");
        }
        else if (kind == FunctionKind.AsyncGenerator)
        {
            // TODO
            // Let prototype be ! OrdinaryObjectCreate(%AsyncGeneratorFunction.prototype.prototype%).
            // Perform DefinePropertyOrThrow(F, "prototype", PropertyDescriptor { [[Value]]: prototype, [[Writable]]: true, [[Enumerable]]: false, [[Configurable]]: false }).
            ExceptionHelper.ThrowNotImplementedException("async generators not implemented");
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
    internal ScriptFunctionInstance OrdinaryFunctionCreate(
        ObjectInstance functionPrototype,
        JintFunctionDefinition function,
        FunctionThisMode thisMode,
        EnvironmentRecord scope,
        PrivateEnvironmentRecord? privateScope)
    {
        return new ScriptFunctionInstance(
            _engine,
            function,
            scope,
            thisMode,
            functionPrototype) { _privateEnvironment = privateScope, _realm = _realm };
    }
}
