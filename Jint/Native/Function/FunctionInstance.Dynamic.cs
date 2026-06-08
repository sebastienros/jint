using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Environment = Jint.Runtime.Environments.Environment;

namespace Jint.Native.Function;

/// <summary>
/// Cache key for repeated new Function(...) compilations; the function kind is embedded in
/// the source prefix so it does not need to be part of the key. Parse-affecting
/// <see cref="ParserOptions"/> are validated on hit via <see cref="DynamicFunctionCacheEntry"/>.
/// </summary>
internal readonly record struct DynamicFunctionCacheKey(string FunctionExpression, bool Strict);

internal sealed class DynamicFunctionCacheEntry
{
    public required ParserOptions ParserOptions { get; init; }
    public required Runtime.Interpreter.JintFunctionDefinition Definition { get; init; }
}

#pragma warning disable MA0049
public partial class Function
#pragma warning restore MA0049
{
    private static readonly JsString _functionNameAnonymous = new JsString("anonymous");

    private const int DynamicFunctionCacheCapacity = 32;
    private const int DynamicFunctionCacheMaxSourceLength = 32 * 1024;

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
                Throw.ArgumentOutOfRangeException(nameof(kind), kind.ToString());
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

        JintFunctionDefinition? definition = null;
        try
        {
            string? functionExpression = null;
            if (argCount == 0)
            {
                switch (kind)
                {
                    case FunctionKind.Normal:
                        functionExpression = "function anonymous(\n) {\n\n}";
                        break;
                    case FunctionKind.Generator:
                        functionExpression = "function* anonymous(\n) {\n\n}";
                        break;
                    case FunctionKind.Async:
                        functionExpression = "async function anonymous(\n) {\n\n}";
                        break;
                    case FunctionKind.AsyncGenerator:
                        functionExpression = "async function* anonymous(\n) {\n\n}";
                        break;
                    default:
                        Throw.ArgumentOutOfRangeException(nameof(kind), kind.ToString());
                        break;
                }
            }
            else
            {
                switch (kind)
                {
                    case FunctionKind.Normal:
                        functionExpression = "function anonymous(";
                        break;
                    case FunctionKind.Async:
                        functionExpression = "async function anonymous(";
                        break;
                    case FunctionKind.Generator:
                        functionExpression = "function* anonymous(";
                        break;
                    case FunctionKind.AsyncGenerator:
                        functionExpression = "async function* anonymous(";
                        break;
                    default:
                        Throw.ArgumentOutOfRangeException(nameof(kind), kind.ToString());
                        break;
                }

                // Per spec (CreateDynamicFunction step 29), a line feed follows the parameters,
                // and the body is wrapped with line feeds (step 16). This ensures HTML-like
                // comments (<!-- and -->) are correctly handled as line comments.
                functionExpression += p + "\n) {\n" + body + "\n}";
            }

            var parserOptions = _engine.GetActiveParserOptions();
            if (!parserOptions.AllowReturnOutsideFunction)
            {
                parserOptions = parserOptions with { AllowReturnOutsideFunction = true };
            }

            // Compilation cache for repeated new Function(...) with identical sources: the parsed
            // function definition is shared (like closures sharing one definition); the resulting
            // Function object below is always a fresh instance. Parse failures are never cached.
            var cacheable = functionExpression!.Length <= DynamicFunctionCacheMaxSourceLength;
            var cacheKey = new DynamicFunctionCacheKey(functionExpression, _engine._isStrict);
            var cache = _realm._dynamicFunctionCache;
            if (cacheable
                && cache is not null
                && cache.TryGetValue(cacheKey, out var cachedEntry)
                && (ReferenceEquals(cachedEntry.ParserOptions, parserOptions) || cachedEntry.ParserOptions.Equals(parserOptions)))
            {
                definition = cachedEntry.Definition;
            }
            else
            {
                Parser parser = new(parserOptions);
                var function = (IFunction) parser.ParseScriptGuarded(callerRealm, functionExpression, strict: _engine._isStrict).Body[0];
                definition = new JintFunctionDefinition(function);

                if (cacheable)
                {
                    // Promote into the cache only on the second sighting of the same source.
                    if (cacheKey.Equals(_realm._dynamicFunctionProbationKey))
                    {
                        cache = _realm._dynamicFunctionCache ??= new Dictionary<DynamicFunctionCacheKey, DynamicFunctionCacheEntry>();
                        if (cache.Count >= DynamicFunctionCacheCapacity)
                        {
                            cache.Clear();
                        }
                        cache[cacheKey] = new DynamicFunctionCacheEntry { ParserOptions = parserOptions, Definition = definition };
                    }
                    else
                    {
                        _realm._dynamicFunctionProbationKey = cacheKey;
                    }
                }
            }
        }
        catch (ParseErrorException ex)
        {
            Throw.SyntaxError(_engine.ExecutionContext.Realm, ex.Message);
        }

        var proto = GetPrototypeFromConstructor(newTarget, fallbackProto);
        var realmF = _realm;
        var scope = realmF.GlobalEnv;
        PrivateEnvironment? privateEnv = null;

        Function F = OrdinaryFunctionCreate(proto, definition!, definition!.Function.IsStrict() ? FunctionThisMode.Strict : FunctionThisMode.Global, scope, privateEnv);
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
            functionPrototype)
        {
            _privateEnvironment = privateScope,
            _realm = _realm,
        };
    }
}
