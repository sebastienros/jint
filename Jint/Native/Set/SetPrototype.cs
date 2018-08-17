using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Set
{
    /// <summary>
    /// https://www.ecma-international.org/ecma-262/6.0/#sec-map-objects
    /// </summary>
    public sealed class SetPrototype : SetInstance
    {
        private SetPrototype(Engine engine) : base(engine)
        {
        }

        public static SetPrototype CreatePrototypeObject(Engine engine, SetConstructor mapConstructor)
        {
            var obj = new SetPrototype(engine)
            {
                Extensible = true, Prototype = engine.Object.PrototypeObject
            };

            obj.SetOwnProperty("length", new PropertyDescriptor(0, PropertyFlag.Configurable));
            obj.SetOwnProperty("constructor", new PropertyDescriptor(mapConstructor, PropertyFlag.NonEnumerable));

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("add", new ClrFunctionInstance(Engine, "add", Add, 0), true, false, true);
            FastAddProperty("clear", new ClrFunctionInstance(Engine, "clear", Clear, 0), true, false, true);
            FastAddProperty("delete", new ClrFunctionInstance(Engine, "delete", Delete, 1), true, false, true);
            FastAddProperty("entries", new ClrFunctionInstance(Engine, "entries", Entries, 0), true, false, true);
            FastAddProperty("forEach", new ClrFunctionInstance(Engine, "forEach", ForEach, 1), true, false, true);
            FastAddProperty("has", new ClrFunctionInstance(Engine, "has", Has, 1), true, false, true);
            FastAddProperty("iterator", new ClrFunctionInstance(Engine, "iterator", Iterator, 1), true, false, true);
            FastAddProperty("values", new ClrFunctionInstance(Engine, "values", Values, 0), true, false, true);
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

        private ObjectInstance Iterator(JsValue thisObj, JsValue[] arguments)
        {
            return ((SetInstance) thisObj).Iterator();
        }

        private ObjectInstance Values(JsValue thisObj, JsValue[] arguments)
        {
            return ((SetInstance) thisObj).Values();
        }
    }
}