using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.WeakMap
{
    /// <summary>
    /// https://262.ecma-international.org/11.0/#sec-weakmap-objects
    /// </summary>
    public sealed class WeakMapPrototype : ObjectInstance
    {
        private WeakMapConstructor _weakMapConstructor;

        private WeakMapPrototype(Engine engine) : base(engine)
        {
        }

        public static WeakMapPrototype CreatePrototypeObject(Engine engine, WeakMapConstructor weakMapConstructor)
        {
            var obj = new WeakMapPrototype(engine)
            {
                _prototype = engine.Object.PrototypeObject,
                _weakMapConstructor = weakMapConstructor
            };

            return obj;
        }

        protected override void Initialize()
        {
            const PropertyFlag propertyFlags = PropertyFlag.Configurable | PropertyFlag.Writable;
            var properties = new PropertyDictionary(6, checkExistingKeys: false)
            {
                ["length"] = new PropertyDescriptor(0, PropertyFlag.Configurable),
                ["constructor"] = new PropertyDescriptor(_weakMapConstructor, PropertyFlag.NonEnumerable),
                ["delete"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "delete", Delete, 1, PropertyFlag.Configurable), propertyFlags),
                ["get"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "get", Get, 1, PropertyFlag.Configurable), propertyFlags),
                ["has"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "has", Has, 1, PropertyFlag.Configurable), propertyFlags),
                ["set"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "set", Set, 2, PropertyFlag.Configurable), propertyFlags),
            };
            SetProperties(properties);
        }

        private JsValue Get(JsValue thisObj, JsValue[] arguments)
        {
            var map = AssertWeakMapInstance(thisObj);
            return map.WeakMapGet(arguments.At(0));
        }

        private JsValue Delete(JsValue thisObj, JsValue[] arguments)
        {
            var map = AssertWeakMapInstance(thisObj);
            return (arguments.Length > 0 && map.WeakMapDelete(arguments.At(0))) ? JsBoolean.True : JsBoolean.False;
        }

        private JsValue Set(JsValue thisObj, JsValue[] arguments)
        {
            var map = AssertWeakMapInstance(thisObj);
            map.WeakMapSet(arguments.At(0), arguments.At(1));
            return thisObj;
        }

        private JsValue Has(JsValue thisObj, JsValue[] arguments)
        {
            var map = AssertWeakMapInstance(thisObj);
            return map.WeakMapHas(arguments.At(0)) ? JsBoolean.True : JsBoolean.False;
        }
        
        private WeakMapInstance AssertWeakMapInstance(JsValue thisObj)
        {
            if (!(thisObj is WeakMapInstance map))
            {
                return ExceptionHelper.ThrowTypeError<WeakMapInstance>(_engine, "object must be a WeakMap");
            }

            return map;
        }
    }
}