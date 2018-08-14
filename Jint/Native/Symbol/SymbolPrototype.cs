using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Symbol
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.5.4
    /// </summary>
    public sealed class SymbolPrototype : ObjectInstance
    {
        private SymbolPrototype(Engine engine)
            : base(engine)
        {
        }

        public static SymbolPrototype CreatePrototypeObject(Engine engine, SymbolConstructor symbolConstructor)
        {
            var obj = new SymbolPrototype(engine);
            obj.Prototype = engine.Object.PrototypeObject;
            obj.Extensible = true;
            obj.SetOwnProperty("length", new PropertyDescriptor(0, PropertyFlag.AllForbidden));
            obj.FastAddProperty("constructor", symbolConstructor, true, false, true);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance(Engine, "toString", ToSymbolString), true, false, true);
            FastAddProperty("valueOf", new ClrFunctionInstance(Engine, "valueOf", ValueOf), true, false, true);
            FastAddProperty("toStringTag", new JsString("Symbol"), false, false, true);

            SetIntrinsicValue(GlobalSymbolRegistry.ToPrimitive, new ClrFunctionInstance(Engine, "toPrimitive", ToPrimitive), false, false, true);
            SetIntrinsicValue(GlobalSymbolRegistry.ToStringTag, new JsString("Symbol"), false, false, true);
        }

        public string SymbolDescriptiveString(JsSymbol sym)
        {
            return $"Symbol({sym.AsSymbol()})";
        }

        private JsValue ToSymbolString(JsValue thisObject, JsValue[] arguments)
        {
            if (!thisObject.IsSymbol())
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            return SymbolDescriptiveString((JsSymbol)thisObject);
        }

        private JsValue ValueOf(JsValue thisObject, JsValue[] arguments)
        {
            var sym = thisObject.TryCast<SymbolInstance>();
            if (ReferenceEquals(sym, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            return sym.SymbolData;
        }

        private JsValue ToPrimitive(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject.IsSymbol())
            {
                return thisObject;
            }

            // Steps 3. and 4.
            var o = thisObject.AsInstance<SymbolInstance>();
            if (ReferenceEquals(o, null))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            return o.SymbolData;
        }

    }
}
