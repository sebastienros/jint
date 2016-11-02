using System;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Symbol
{
    /// <summary>
    /// 19.4
    /// http://www.ecma-international.org/ecma-262/6.0/index.html#sec-symbol-objects
    /// </summary>
    public sealed class SymbolConstructor : FunctionInstance, IConstructor
    {
        public SymbolConstructor(Engine engine)
            : base(engine, null, null, false)
        {
        }

        public static SymbolConstructor CreateSymbolConstructor(Engine engine)
        {
            var obj = new SymbolConstructor(engine);
            obj.Extensible = true;

            // The value of the [[Prototype]] internal property of the Symbol constructor is the Function prototype object
            obj.Prototype = engine.Function.PrototypeObject;
            obj.PrototypeObject = SymbolPrototype.CreatePrototypeObject(engine, obj);

            obj.FastAddProperty("length", 0, false, false, false);

            // The initial value of String.prototype is the String prototype object
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);


            return obj;
        }

        public void Configure()
        {
            FastAddProperty("for", new ClrFunctionInstance(Engine, For, 1), true, false, true);
            FastAddProperty("keyFor", new ClrFunctionInstance(Engine, KeyFor, 1), true, false, true);

            FastAddProperty("hasInstance", GlobalSymbolRegistry.HasInstance, false, false, false);
            FastAddProperty("isConcatSpreadable", GlobalSymbolRegistry.IsConcatSpreadable, false, false, false);
            FastAddProperty("iterator", GlobalSymbolRegistry.Iterator, false, false, false);
            FastAddProperty("match", GlobalSymbolRegistry.Match, false, false, false);
            FastAddProperty("replace", GlobalSymbolRegistry.Replace, false, false, false);
            FastAddProperty("search", GlobalSymbolRegistry.Search, false, false, false);
            FastAddProperty("species", GlobalSymbolRegistry.Species, false, false, false);
            FastAddProperty("split", GlobalSymbolRegistry.Split, false, false, false);
            FastAddProperty("toPrimitive", GlobalSymbolRegistry.ToPrimitive, false, false, false);
            FastAddProperty("toStringTag", GlobalSymbolRegistry.ToStringTag, false, false, false);
            FastAddProperty("unscopables", GlobalSymbolRegistry.Unscopables, false, false, false);

        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/6.0/index.html#sec-symbol-description
        /// </summary>
        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            var description = arguments.At(0);
            var descString = description == Undefined.Instance
                ? Undefined.Instance
                : TypeConverter.ToString(description);

            var value = new JsSymbol(description.AsString());

            if (JsValue.ReturnOnAbruptCompletion(ref descString))
            {
                return descString;
            }

            return value;
        }

        public JsValue For(JsValue thisObj, JsValue[] arguments)
        {
            var stringKey = TypeConverter.ToString(arguments.At(0));

            // 2. ReturnIfAbrupt(stringKey).

            JsSymbol symbol;
            if (!Engine.GlobalSymbolRegistry.TryGetValue(stringKey, out symbol))
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
                throw new JavaScriptException(Engine.TypeError);
            }

            JsSymbol symbol;
            if (!Engine.GlobalSymbolRegistry.TryGetValue(sym.AsSymbol(), out symbol))
            {
                return Undefined.Instance;
            }

            return sym.AsSymbol();
        }

        public ObjectInstance Construct(JsValue[] arguments)
        {
            throw new JavaScriptException(Engine.TypeError);
        }

        public SymbolInstance Construct(string description)
        {
            var instance = new SymbolInstance(Engine);
            instance.Prototype = PrototypeObject;
            instance.SymbolData = new JsSymbol(description);
            instance.Extensible = true;

            return instance;
        }

        public SymbolPrototype PrototypeObject { get; private set; }
    }
}
