#pragma warning disable CA1859 // Use concrete types when possible for improved performance -- most of constructor methods return JsValue

using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Symbol;

/// <summary>
/// 19.4
/// http://www.ecma-international.org/ecma-262/6.0/index.html#sec-symbol-objects
/// </summary>
internal sealed class SymbolConstructor : Constructor
{
    private static readonly JsString _functionName = new JsString("Symbol");

    internal SymbolConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new SymbolPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    public SymbolPrototype PrototypeObject { get; }

    protected override void Initialize()
    {
        const PropertyFlag lengthFlags = PropertyFlag.Configurable;
        const PropertyFlag propertyFlags = PropertyFlag.AllForbidden;

        var properties = new PropertyDictionary(15, checkExistingKeys: false)
        {
            ["for"] = new PropertyDescriptor(new ClrFunction(Engine, "for", For, 1, lengthFlags), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["keyFor"] = new PropertyDescriptor(new ClrFunction(Engine, "keyFor", KeyFor, 1, lengthFlags), PropertyFlag.Writable | PropertyFlag.Configurable),
            ["hasInstance"] = new PropertyDescriptor(GlobalSymbolRegistry.HasInstance, propertyFlags),
            ["isConcatSpreadable"] = new PropertyDescriptor(GlobalSymbolRegistry.IsConcatSpreadable, propertyFlags),
            ["iterator"] = new PropertyDescriptor(GlobalSymbolRegistry.Iterator, propertyFlags),
            ["match"] = new PropertyDescriptor(GlobalSymbolRegistry.Match, propertyFlags),
            ["matchAll"] = new PropertyDescriptor(GlobalSymbolRegistry.MatchAll, propertyFlags),
            ["replace"] = new PropertyDescriptor(GlobalSymbolRegistry.Replace, propertyFlags),
            ["search"] = new PropertyDescriptor(GlobalSymbolRegistry.Search, propertyFlags),
            ["species"] = new PropertyDescriptor(GlobalSymbolRegistry.Species, propertyFlags),
            ["split"] = new PropertyDescriptor(GlobalSymbolRegistry.Split, propertyFlags),
            ["toPrimitive"] = new PropertyDescriptor(GlobalSymbolRegistry.ToPrimitive, propertyFlags),
            ["toStringTag"] = new PropertyDescriptor(GlobalSymbolRegistry.ToStringTag, propertyFlags),
            ["unscopables"] = new PropertyDescriptor(GlobalSymbolRegistry.Unscopables, propertyFlags),
            ["asyncIterator"] = new PropertyDescriptor(GlobalSymbolRegistry.AsyncIterator, propertyFlags)
        };
        SetProperties(properties);
    }

    /// <summary>
    /// http://www.ecma-international.org/ecma-262/6.0/index.html#sec-symbol-description
    /// </summary>
    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        var description = arguments.At(0);
        var descString = description.IsUndefined()
            ? Undefined
            : TypeConverter.ToJsString(description);

        var value = GlobalSymbolRegistry.CreateSymbol(descString);
        return value;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-symbol.for
    /// </summary>
    private JsValue For(JsValue thisObject, JsCallArguments arguments)
    {
        var stringKey = TypeConverter.ToJsString(arguments.At(0));

        // 2. ReturnIfAbrupt(stringKey).

        if (!_engine.GlobalSymbolRegistry.TryGetSymbol(stringKey, out var symbol))
        {
            symbol = GlobalSymbolRegistry.CreateSymbol(stringKey);
            _engine.GlobalSymbolRegistry.Add(symbol);
        }

        return symbol;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-symbol.keyfor
    /// </summary>
    private JsValue KeyFor(JsValue thisObject, JsCallArguments arguments)
    {
        var symbol = arguments.At(0) as JsSymbol;
        if (symbol is null)
        {
            ExceptionHelper.ThrowTypeError(_realm);
        }

        if (_engine.GlobalSymbolRegistry.TryGetSymbol(symbol._value, out var e))
        {
            return e._value;
        }

        return Undefined;
    }

    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        ExceptionHelper.ThrowTypeError(_realm, "Symbol is not a constructor");
        return null;
    }

    public SymbolInstance Construct(JsSymbol symbol)
    {
        return new SymbolInstance(Engine, PrototypeObject, symbol);
    }
}
