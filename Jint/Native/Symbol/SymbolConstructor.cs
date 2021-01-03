using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Symbol
{
    /// <summary>
    /// 19.4
    /// http://www.ecma-international.org/ecma-262/6.0/index.html#sec-symbol-objects
    /// </summary>
    public sealed class SymbolConstructor : FunctionInstance, IConstructor
    {
        private static readonly JsString _functionName = new JsString("Symbol");

        public SymbolConstructor(Engine engine)
            : base(engine, _functionName, FunctionThisMode.Global)
        {
        }

        public static SymbolConstructor CreateSymbolConstructor(Engine engine)
        {
            var obj = new SymbolConstructor(engine)
            {
                _prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the Symbol constructor is the Function prototype object
            obj.PrototypeObject = SymbolPrototype.CreatePrototypeObject(engine, obj);

            obj._length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);

            // The initial value of String.prototype is the String prototype object
            obj._prototypeDescriptor = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        protected override void Initialize()
        {
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            const PropertyFlag propertyFlags = PropertyFlag.AllForbidden;

            var properties = new PropertyDictionary(15, checkExistingKeys: false)
            {
                ["for"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "for", For, 1, lengthFlags), PropertyFlag.Writable | PropertyFlag.Configurable),
                ["keyFor"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "keyFor", KeyFor, 1, lengthFlags), PropertyFlag.Writable | PropertyFlag.Configurable),
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
        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            var description = arguments.At(0);
            var descString = description.IsUndefined()
                ? Undefined
                : TypeConverter.ToJsString(description);

            var value = _engine.GlobalSymbolRegistry.CreateSymbol(descString);
            return value;
        }

        public JsValue For(JsValue thisObj, JsValue[] arguments)
        {
            var stringKey = TypeConverter.ToJsString(arguments.At(0));

            // 2. ReturnIfAbrupt(stringKey).

            if (!_engine.GlobalSymbolRegistry.TryGetSymbol(stringKey, out var symbol))
            {
                symbol = _engine.GlobalSymbolRegistry.CreateSymbol(stringKey);
                _engine.GlobalSymbolRegistry.Add(symbol);
            }

            return symbol;
        }

        public JsValue KeyFor(JsValue thisObj, JsValue[] arguments)
        {
            if (!(arguments.At(0) is JsSymbol sym))
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(Engine);
            }

            if (_engine.GlobalSymbolRegistry.TryGetSymbol(sym._value, out var e))
            {
                return e._value;
            }

            return Undefined;
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            return ExceptionHelper.ThrowTypeError<ObjectInstance>(Engine);
        }

        public SymbolInstance Construct(JsSymbol symbol)
        {
            var instance = new SymbolInstance(Engine)
            {
                _prototype = PrototypeObject,
                SymbolData = symbol
            };

            return instance;
        }

        public SymbolPrototype PrototypeObject { get; private set; }
    }
}
