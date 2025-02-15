using System.Text.RegularExpressions;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.RegExp;

public sealed class RegExpConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("RegExp");

    internal RegExpConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new RegExpPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(2, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal RegExpPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.Species] = new GetSetPropertyDescriptor(get: new ClrFunction(_engine, "get [Symbol.species]", (thisObj, _) => thisObj, 0, PropertyFlag.Configurable), set: Undefined, PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        return Construct(arguments, thisObject);
    }

    public ObjectInstance Construct(JsCallArguments arguments)
    {
        return Construct(arguments, this);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-regexp-pattern-flags
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var pattern = arguments.At(0);
        var flags = arguments.At(1);

        var patternIsRegExp = pattern.IsRegExp();
        if (newTarget.IsUndefined())
        {
            newTarget = this;
            if (patternIsRegExp && flags.IsUndefined())
            {
                var patternConstructor = pattern.Get(CommonProperties.Constructor);
                if (ReferenceEquals(newTarget, patternConstructor))
                {
                    return (ObjectInstance) pattern;
                }
            }
        }

        JsValue p;
        JsValue f;
        if (pattern is JsRegExp regExpInstance)
        {
            p = regExpInstance.Source;
            f = flags.IsUndefined() ? regExpInstance.Flags : flags;
        }
        else if (patternIsRegExp)
        {
            p = pattern.Get(RegExpPrototype.PropertySource);
            f = flags.IsUndefined() ? pattern.Get(RegExpPrototype.PropertyFlags) : flags;
        }
        else
        {
            p = pattern;
            f = flags;
        }

        var r = RegExpAlloc(newTarget);
        return RegExpInitialize(r, p, f);
    }

    private JsRegExp RegExpInitialize(JsRegExp r, JsValue pattern, JsValue flags)
    {
        var p = pattern.IsUndefined() ? "" : TypeConverter.ToString(pattern);
        if (string.IsNullOrEmpty(p))
        {
            p = "(?:)";
        }

        var f = flags.IsUndefined() ? "" : TypeConverter.ToString(flags);

        var parserOptions = _engine.GetActiveParserOptions();
        try
        {
            var regExpParseResult = Tokenizer.AdaptRegExp(p, f, compiled: false, parserOptions.RegexTimeout,
                ecmaVersion: parserOptions.EcmaVersion,
                experimentalESFeatures: parserOptions.ExperimentalESFeatures);

            if (!regExpParseResult.Success)
            {
                ExceptionHelper.ThrowSyntaxError(_realm, $"Unsupported regular expression. {regExpParseResult.ConversionError!.Description}");
            }

            r.Value = regExpParseResult.Regex!;
            r.ParseResult = regExpParseResult;
        }
        catch (Exception ex)
        {
            ExceptionHelper.ThrowSyntaxError(_realm, ex.Message);
        }

        r.Flags = f;
        r.Source = p;

        RegExpInitialize(r);

        return r;
    }

    private JsRegExp RegExpAlloc(JsValue newTarget)
    {
        var r = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.RegExp.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new JsRegExp(engine));
        return r;
    }

    public JsRegExp Construct(Regex regExp, string source, string flags, RegExpParseResult regExpParseResult = default)
    {
        var r = RegExpAlloc(this);
        r.Value = regExp;
        r.Source = source;
        r.Flags = flags;
        r.ParseResult = regExpParseResult;

        RegExpInitialize(r);

        return r;
    }

    private static void RegExpInitialize(JsRegExp r)
    {
        r.SetOwnProperty(JsRegExp.PropertyLastIndex, new PropertyDescriptor(0, PropertyFlag.OnlyWritable));
    }
}
