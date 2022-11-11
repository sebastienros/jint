using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Symbol
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-properties-of-the-symbol-prototype-object
    /// </summary>
    internal sealed class SymbolPrototype : Prototype
    {
        private readonly SymbolConstructor _constructor;

        internal SymbolPrototype(
            Engine engine,
            Realm realm,
            SymbolConstructor symbolConstructor,
            ObjectPrototype objectPrototype)
            : base(engine, realm)
        {
            _prototype = objectPrototype;
            _constructor = symbolConstructor;
        }

        protected override void Initialize()
        {
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            const PropertyFlag propertyFlags = PropertyFlag.Configurable;
            SetProperties(new PropertyDictionary(5, checkExistingKeys: false)
            {
                ["length"] = new PropertyDescriptor(JsNumber.PositiveZero, propertyFlags),
                ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.Configurable | PropertyFlag.Writable),
                ["description"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(Engine, "description", Description, 0, lengthFlags), Undefined, propertyFlags),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToSymbolString, 0, lengthFlags), PropertyFlag.Configurable | PropertyFlag.Writable),
                ["valueOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "valueOf", ValueOf, 0, lengthFlags), PropertyFlag.Configurable | PropertyFlag.Writable)
            });

            SetSymbols(new SymbolDictionary(1)
                {
                    [GlobalSymbolRegistry.ToPrimitive] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "[Symbol.toPrimitive]", ToPrimitive, 1, lengthFlags), propertyFlags), [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor(new JsString("Symbol"), propertyFlags)
                }
            );
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-symbol.prototype.description
        /// </summary>
        private JsValue Description(JsValue thisObject, JsValue[] arguments)
        {
            var sym = ThisSymbolValue(thisObject);
            return sym._value;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-symbol.prototype.tostring
        /// </summary>
        private JsValue ToSymbolString(JsValue thisObject, JsValue[] arguments)
        {
            var sym = ThisSymbolValue(thisObject);
            return new JsString(sym.ToString());
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-symbol.prototype.valueof
        /// </summary>
        private JsValue ValueOf(JsValue thisObject, JsValue[] arguments)
        {
            return ThisSymbolValue(thisObject);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-symbol.prototype-@@toprimitive
        /// </summary>
        private JsValue ToPrimitive(JsValue thisObject, JsValue[] arguments)
        {
            return ThisSymbolValue(thisObject);
        }

        private JsSymbol ThisSymbolValue(JsValue thisObject)
        {
            if (thisObject is JsSymbol s)
            {
                return s;
            }

            if (thisObject is SymbolInstance instance)
            {
                return instance.SymbolData;
            }

            ExceptionHelper.ThrowTypeError(_realm);
            return null;
        }
    }
}
