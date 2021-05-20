using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Map
{
    /// <summary>
    /// https://www.ecma-international.org/ecma-262/6.0/#sec-map-objects
    /// </summary>
    public sealed class MapPrototype : ObjectInstance
    {
        private MapConstructor _mapConstructor;

        private MapPrototype(Engine engine) : base(engine)
        {
        }

        public static MapPrototype CreatePrototypeObject(Engine engine, MapConstructor mapConstructor)
        {
            var obj = new MapPrototype(engine)
            {
                _prototype = engine.Object.PrototypeObject,
                _mapConstructor = mapConstructor
            };

            return obj;
        }

        protected override void Initialize()
        {
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            var properties = new PropertyDictionary(12, checkExistingKeys: false)
            {
                ["length"] = new PropertyDescriptor(0, PropertyFlag.Configurable),
                ["constructor"] = new PropertyDescriptor(_mapConstructor, PropertyFlag.NonEnumerable),
                ["clear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "clear", Clear, 0, PropertyFlag.Configurable), propertyFlags),
                ["delete"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "delete", Delete, 1, PropertyFlag.Configurable), propertyFlags),
                ["entries"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "entries", Entries, 0, PropertyFlag.Configurable), propertyFlags),
                ["forEach"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "forEach", ForEach, 1, PropertyFlag.Configurable), propertyFlags),
                ["get"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "get", Get, 1, PropertyFlag.Configurable), propertyFlags),
                ["has"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "has", Has, 1, PropertyFlag.Configurable), propertyFlags),
                ["keys"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "keys", Keys, 0, PropertyFlag.Configurable), propertyFlags),
                ["set"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "set", Set, 2, PropertyFlag.Configurable), propertyFlags),
                ["values"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "values", Values, 0, PropertyFlag.Configurable), propertyFlags),
                ["size"] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(Engine, "get size", Size, 0, PropertyFlag.Configurable), set: null, PropertyFlag.Configurable)
            };
            SetProperties(properties);

            var symbols = new SymbolDictionary(2)
            {
                [GlobalSymbolRegistry.Iterator] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "iterator", Entries, 1, PropertyFlag.Configurable), propertyFlags),
                [GlobalSymbolRegistry.ToStringTag] = new PropertyDescriptor("Map", false, false, true),
            };
            SetSymbols(symbols);
        }

        private JsValue Size(JsValue thisObj, JsValue[] arguments)
        {
            AssertMapInstance(thisObj);
            return JsNumber.Create(0);
        }

        private JsValue Get(JsValue thisObj, JsValue[] arguments)
        {
            var map = AssertMapInstance(thisObj);
            return map.MapGet(arguments.At(0));
        }

        private JsValue Clear(JsValue thisObj, JsValue[] arguments)
        {
            var map = AssertMapInstance(thisObj);
            map.Clear();
            return Undefined;
        }

        private JsValue Delete(JsValue thisObj, JsValue[] arguments)
        {
            var map = AssertMapInstance(thisObj);
            return map.MapDelete(arguments[0])
                ? JsBoolean.True
                : JsBoolean.False;
        }

        private JsValue Set(JsValue thisObj, JsValue[] arguments)
        {
            var map = AssertMapInstance(thisObj);
            map.MapSet(arguments[0], arguments[1]);
            return thisObj;
        }

        private JsValue Has(JsValue thisObj, JsValue[] arguments)
        {
            var map = AssertMapInstance(thisObj);
            return map.Has(arguments[0])
                ? JsBoolean.True
                : JsBoolean.False;
        }

        private JsValue ForEach(JsValue thisObj, JsValue[] arguments)
        {
            var map = AssertMapInstance(thisObj);
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var callable = GetCallable(callbackfn);

            map.ForEach(callable, thisArg);

            return Undefined;
        }

        private ObjectInstance Entries(JsValue thisObj, JsValue[] arguments)
        {
            var map = AssertMapInstance(thisObj);
            return map.Iterator();
        }

        private ObjectInstance Keys(JsValue thisObj, JsValue[] arguments)
        {
            var map = AssertMapInstance(thisObj);
            return map.Keys();
        }

        private ObjectInstance Values(JsValue thisObj, JsValue[] arguments)
        {
            var map = AssertMapInstance(thisObj);
            return map.Values();
        }

        private MapInstance AssertMapInstance(JsValue thisObj)
        {
            if (!(thisObj is MapInstance map))
            {
                return ExceptionHelper.ThrowTypeError<MapInstance>(_engine, "object must be a Map");
            }

            return map;
        }
    }
}