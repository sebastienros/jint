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
            : base(engine, _functionName, strict: false)
        {
        }

        public static SymbolConstructor CreateSymbolConstructor(Engine engine)
        {
            var obj = new SymbolConstructor(engine)
            {
                Extensible = true,
                Prototype = engine.Function.PrototypeObject
            };

            // The value of the [[Prototype]] internal property of the Symbol constructor is the Function prototype object
            obj.PrototypeObject = SymbolPrototype.CreatePrototypeObject(engine, obj);

            obj._length = PropertyDescriptor.AllForbiddenDescriptor.NumberZero;

            // The initial value of String.prototype is the String prototype object
            obj._prototype = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);

            return obj;
        }

        protected override void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(15)
            {
                ["species"] = new PropertyDescriptor(GlobalSymbolRegistry.Species, PropertyFlag.AllForbidden),
                ["for"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "for", For, 1), true, false, true),
                ["keyFor"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "keyFor", KeyFor, 1), true, false, true),
                ["hasInstance"] = new PropertyDescriptor(GlobalSymbolRegistry.HasInstance, false, false, false),
                ["isConcatSpreadable"] = new PropertyDescriptor(GlobalSymbolRegistry.IsConcatSpreadable, false, false, false),
                ["iterator"] = new PropertyDescriptor(GlobalSymbolRegistry.Iterator, false, false, false),
                ["match"] = new PropertyDescriptor(GlobalSymbolRegistry.Match, false, false, false),
                ["replace"] = new PropertyDescriptor(GlobalSymbolRegistry.Replace, false, false, false),
                ["search"] = new PropertyDescriptor(GlobalSymbolRegistry.Search, false, false, false),
                ["species"] = new PropertyDescriptor(GlobalSymbolRegistry.Species, false, false, false),
                ["split"] = new PropertyDescriptor(GlobalSymbolRegistry.Split, false, false, false),
                ["toPrimitive"] = new PropertyDescriptor(GlobalSymbolRegistry.ToPrimitive, false, false, false),
                ["toStringTag"] = new PropertyDescriptor(GlobalSymbolRegistry.ToStringTag, false, false, false),
                ["unscopables"] = new PropertyDescriptor(GlobalSymbolRegistry.Unscopables, false, false, false)
            };
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/6.0/index.html#sec-symbol-description
        /// </summary>
        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            var description = arguments.At(0);
            var descString = description.IsUndefined()
                ? Undefined
                : TypeConverter.ToString(description);

            if (ReturnOnAbruptCompletion(ref descString))
            {
                return descString;
            }

            var value = new JsSymbol(description);
            return value;
        }

        public JsValue For(JsValue thisObj, JsValue[] arguments)
        {
            var stringKey = TypeConverter.ToString(arguments.At(0));

            // 2. ReturnIfAbrupt(stringKey).

            if (!Engine.GlobalSymbolRegistry.TryGetValue(stringKey, out var symbol))
            {
                symbol = new JsSymbol(stringKey);
                Engine.GlobalSymbolRegistry.Add(stringKey, symbol);
            }

            return symbol;
        }

        public JsValue KeyFor(JsValue thisObj, JsValue[] arguments)
        {
            var sym = arguments.At(0);

            if (!sym.IsSymbol())
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            JsSymbol symbol;
            if (!Engine.GlobalSymbolRegistry.TryGetValue(sym.AsSymbol(), out symbol))
            {
                return Undefined;
            }

            return sym.AsSymbol();
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            ExceptionHelper.ThrowTypeError(Engine);
            return null;
        }

        public SymbolInstance Construct(string description)
        {
            return Construct(new JsSymbol(description));
        }

        public SymbolInstance Construct(JsSymbol symbol)
        {
            var instance = new SymbolInstance(Engine)
            {
                Prototype = PrototypeObject,
                SymbolData = symbol,
                Extensible = true
            };

            return instance;
        }

        public SymbolPrototype PrototypeObject { get; private set; }
    }
}
