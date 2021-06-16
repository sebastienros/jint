using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.WeakSet
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-weakset-objects
    /// </summary>
    public sealed class WeakSetPrototype : Prototype
    {
        private readonly WeakSetConstructor _constructor;

        internal WeakSetPrototype(
            Engine engine,
            Realm realm,
            WeakSetConstructor constructor,
            ObjectPrototype prototype) : base(engine, realm)
        {
            _prototype = prototype;
            _constructor = constructor;
        }

        protected override void Initialize()
        {
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            var properties = new PropertyDictionary(5, checkExistingKeys: false)
            {
                ["length"] = new PropertyDescriptor(0, PropertyFlag.Configurable),
                ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
                ["delete"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "delete", Delete, 1, PropertyFlag.Configurable), propertyFlags),
                ["add"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "add", Add, 1, PropertyFlag.Configurable), propertyFlags),
                ["has"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "has", Has, 1, PropertyFlag.Configurable), propertyFlags),
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(1)
            {
                [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("WeakSet", false, false, true)
            };
            SetSymbols(symbols);
        }

        private JsValue Add(JsValue thisObj, JsValue[] arguments)
        {
            var set = AssertWeakSetInstance(thisObj);
            set.WeakSetAdd(arguments.At(0));
            return thisObj;
        }

        private JsValue Delete(JsValue thisObj, JsValue[] arguments)
        {
            var set = AssertWeakSetInstance(thisObj);
            return set.WeakSetDelete(arguments.At(0)) ? JsBoolean.True : JsBoolean.False;
        }

        private JsValue Has(JsValue thisObj, JsValue[] arguments)
        {
            var set = AssertWeakSetInstance(thisObj);
            return set.WeakSetHas(arguments.At(0)) ? JsBoolean.True : JsBoolean.False;
        }

        private WeakSetInstance AssertWeakSetInstance(JsValue thisObj)
        {
            var set = thisObj as WeakSetInstance;
            if (set is null)
            {
                ExceptionHelper.ThrowTypeError(_realm, "object must be a WeakSet");
            }

            return set;
        }
    }
}