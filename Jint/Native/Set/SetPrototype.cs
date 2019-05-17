using Jint.Collections;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.Set
{
    /// <summary>
    /// https://www.ecma-international.org/ecma-262/6.0/#sec-set-objects
    /// </summary>
    public sealed class SetPrototype : ObjectInstance
    {
        private SetConstructor _mapConstructor;

        private SetPrototype(Engine engine) : base(engine)
        {
        }

        public static SetPrototype CreatePrototypeObject(Engine engine, SetConstructor mapConstructor)
        {
            var obj = new SetPrototype(engine)
            {
                Extensible = true, 
                Prototype = engine.Object.PrototypeObject,
                _mapConstructor = mapConstructor
            };

            return obj;
        }

        protected override void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(15)
            {
                ["length"] = new PropertyDescriptor(0, PropertyFlag.Configurable),
                ["constructor"] = new PropertyDescriptor(_mapConstructor, PropertyFlag.NonEnumerable),
                [GlobalSymbolRegistry.Iterator._value] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "iterator", Values, 1, PropertyFlag.Configurable), true, false, true),
                [GlobalSymbolRegistry.ToStringTag._value] = new PropertyDescriptor("Set", false, false, true),
                ["add"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "add", Add, 1, PropertyFlag.Configurable), true, false, true),
                ["clear"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "clear", Clear, 0, PropertyFlag.Configurable), true, false, true),
                ["delete"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "delete", Delete, 1, PropertyFlag.Configurable), true, false, true),
                ["entries"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "entries", Entries, 0, PropertyFlag.Configurable), true, false, true),
                ["forEach"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "forEach", ForEach, 1, PropertyFlag.Configurable), true, false, true),
                ["has"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "has", Has, 1, PropertyFlag.Configurable), true, false, true),
                ["keys"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "keys", Values, 0, PropertyFlag.Configurable), true, false, true),
                ["values"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "values", Values, 0, PropertyFlag.Configurable), true, false, true),
                ["size"] = new GetSetPropertyDescriptor(get: new ClrFunctionInstance(Engine, "get size", Size, 0, PropertyFlag.Configurable), set: null, PropertyFlag.Configurable)
            };
        }
        
        private JsValue Size(JsValue thisObj, JsValue[] arguments)
        {
            if (!thisObj.IsObject())
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }
            return JsNumber.Create(0);
        }

        private JsValue Add(JsValue thisObj, JsValue[] arguments)
        {
            ((SetInstance) thisObj).Add(arguments[0]);
            return thisObj;
        }

        private JsValue Clear(JsValue thisObj, JsValue[] arguments)
        {
            var map = thisObj as SetInstance
                      ?? ExceptionHelper.ThrowTypeError<SetInstance>(_engine, "object must be a Set");

            map.Clear();
            return Undefined;
        }

        private JsValue Delete(JsValue thisObj, JsValue[] arguments)
        {
            var map = thisObj as SetInstance
                      ?? ExceptionHelper.ThrowTypeError<SetInstance>(_engine, "object must be a Set");

            return map.Delete(arguments[0])
                ? JsBoolean.True
                : JsBoolean.False;
        }

        private JsValue Has(JsValue thisObj, JsValue[] arguments)
        {
            return ((SetInstance) thisObj).Has(arguments[0])
                ? JsBoolean.True
                : JsBoolean.False;
        }

        private JsValue Entries(JsValue thisObj, JsValue[] arguments)
        {
            var map = thisObj as SetInstance
                      ?? ExceptionHelper.ThrowTypeError<SetInstance>(_engine, "object must be a Set");

            return map.Entries();
        }

        private JsValue ForEach(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var map = (SetInstance) thisObj;

            var callable = GetCallable(callbackfn);

            map.ForEach(callable, thisArg);

            return Undefined;
        }

        private ObjectInstance Values(JsValue thisObj, JsValue[] arguments)
        {
            return ((SetInstance) thisObj).Values();
        }
    }
}