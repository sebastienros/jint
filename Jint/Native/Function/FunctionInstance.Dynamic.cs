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
    public required ParsingConstraints ParsingConstraints { get; init; }
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
        if (argCount > 1)
        {
            var prefixLength = kind switch
            {
                FunctionKind.Normal => "function anonymous(".Length,
                FunctionKind.Async => "async function anonymous(".Length,
                FunctionKind.Generator => "function* anonymous(".Length,
                FunctionKind.AsyncGenerator => "async function* anonymous(".Length,
                _ => 0,
            };
            _engine.CheckParsingSourceLength(prefixLength + (long) argCount - 2 + 7);
        }

        var body = "";
        string[]? parameters = null;
        long parametersLength = 0;

        if (argCount == 1)
        {
            body = TypeConverter.ToString(arguments[0]);
        }
        else if (argCount > 1)
        {
            parameters = new string[argCount - 1];
            for (var k = 0; k < parameters.Length; k++)
            {
                var parameter = TypeConverter.ToString(arguments[k]);
                parameters[k] = parameter;
                parametersLength += parameter.Length;
            }

            parametersLength += parameters.Length - 1;
            body = TypeConverter.ToString(arguments[argCount - 1]);
        }

        JintFunctionDefinition? definition = null;
        try
        {
            string? prefix = null;
            switch (kind)
            {
                case FunctionKind.Normal:
                    prefix = "function anonymous(";
                    break;
                case FunctionKind.Async:
                    prefix = "async function anonymous(";
                    break;
                case FunctionKind.Generator:
                    prefix = "function* anonymous(";
                    break;
                case FunctionKind.AsyncGenerator:
                    prefix = "async function* anonymous(";
                    break;
                default:
                    Throw.ArgumentOutOfRangeException(nameof(kind), kind.ToString());
                    break;
            }

            // Per spec (CreateDynamicFunction step 15), a line feed follows the parameters, and the
            // body is wrapped with line feeds (step 14). This ensures HTML-like comments (<!-- and -->)
            // are correctly handled as line comments.
            // The bound is checked against the lengths first: the concatenation below is the largest
            // allocation this path makes, so a source over the limit must be refused before it exists.
            _engine.CheckParsingSourceLength(prefix!.Length + parametersLength + 7L + body.Length);
            var p = parameters is null ? "" : string.Join(',', parameters);
            var functionExpression = prefix + p + "\n) {\n" + body + "\n}";

            _engine.CheckParsingSourceLength(functionExpression!.Length);

            var parserOptions = _engine.GetActiveParserOptions();
            var parsingConstraints = _engine.GetActiveParsingConstraints();
            if (!parserOptions.AllowReturnOutsideFunction)
            {
                parserOptions = parserOptions with { AllowReturnOutsideFunction = true };
            }

            // Compilation cache for repeated new Function(...) with identical sources: the parsed
            // function definition is shared (like closures sharing one definition); the resulting
            // Function object below is always a fresh instance. Parse failures are never cached.
            var cacheable = functionExpression.Length <= DynamicFunctionCacheMaxSourceLength;
            var cacheKey = new DynamicFunctionCacheKey(functionExpression, _engine._isStrict);
            var cache = _realm._dynamicFunctionCache;
            if (cacheable
                && cache is not null
                && cache.TryGetValue(cacheKey, out var cachedEntry)
                && (ReferenceEquals(cachedEntry.ParserOptions, parserOptions) || cachedEntry.ParserOptions.Equals(parserOptions))
                && cachedEntry.ParsingConstraints.Equals(parsingConstraints))
            {
                definition = cachedEntry.Definition;
            }
            else
            {
                // The pooled parser carries the active ParsingConstraints, so the AST-size and depth
                // bounds apply to a dynamically created function exactly as they do to a top-level script.
                var parser = _engine.GetParserFor(parserOptions);
                // CreateDynamicFunction step 24 throws its SyntaxError in the current realm, and the
                // current realm while a built-in runs is the built-in's own [[Realm]] — not the caller's.
                // Jint does not push an execution context for a built-in call, so _engine.ExecutionContext
                // still describes the caller here and only _realm names the callee.
                var script = parser.ParseScriptGuarded(_realm, functionExpression, strict: _engine._isStrict);
                var function = ValidateDynamicFunctionShape(script, prefix!.Length, p.Length);
                definition = new JintFunctionDefinition(function)
                {
                    IsDynamic = true,
                };

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
                        cache[cacheKey] = new DynamicFunctionCacheEntry
                        {
                            ParserOptions = parserOptions,
                            ParsingConstraints = parsingConstraints,
                            Definition = definition
                        };
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
            Throw.SyntaxError(_realm, ex.Message);
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
    /// Enforces CreateDynamicFunction steps 17-24 for a source assembled by
    /// <see cref="CreateDynamicFunction"/>: the spec parses <c>paramString</c> and
    /// <c>bodyParseString</c> on their own (steps 17-20) before parsing the assembled source as a
    /// single function expression (steps 23-24), "to ensure that each is valid alone. For example,
    /// new Function("/*", "*/ ) {") does not evaluate to a function."
    /// https://tc39.es/ecma262/#sec-createdynamicfunction
    /// </summary>
    /// <remarks>
    /// Reparsing both halves would cost two extra parses on every <c>new Function(...)</c> that misses
    /// the compilation cache, so the single parse is checked against the two source positions the
    /// assembled text fixed in advance — which is exactly as strict, because the assembly interleaves
    /// nothing else with the two argument strings.
    /// <list type="bullet">
    /// <item>The body's <c>{</c> can only sit at <c>prefixLength + parameterLength + 3</c> when the
    /// <c>)</c> the spec inserted after the parameters is the one that closed the parameter list, so
    /// the text the parser accepted as parameters is exactly the parameter string. A smaller offset
    /// means the parameter string closed the list itself; a larger one means it swallowed the inserted
    /// <c>)</c> inside an unterminated comment, template or nested parenthesis.</item>
    /// <item>A parse yielding more than one statement is a body string that closed the function early
    /// and continued with statements of its own, which a standalone <c>FunctionBody</c> parse rejects
    /// and which step 23's single <c>FunctionExpression</c> goal rejects as well.</item>
    /// </list>
    /// </remarks>
    private IFunction ValidateDynamicFunctionShape(Script script, int prefixLength, int parameterLength)
    {
        var parsed = script.Body.Count == 1 ? script.Body[0] as IFunction : null;
        if (parsed is null)
        {
            Throw.SyntaxError(_realm, "Function body string is not a complete function body");
        }

        // prefixLength + parameterLength addresses the line feed the assembly inserted after the
        // parameters; the ')' and the space follow it, so the body's '{' is three characters further on.
        var expectedBodyStart = prefixLength + parameterLength + 3;
        var actualBodyStart = parsed.Body.Start;
        if (actualBodyStart != expectedBodyStart)
        {
            Throw.SyntaxError(_realm, actualBodyStart < expectedBodyStart
                ? "Function parameter string terminates the parameter list early"
                : "Function parameter string is not a complete parameter list");
        }

        return parsed;
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
