using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Array
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.4.4
    /// </summary>
    public sealed class ArrayPrototype : ObjectInstance
    {
        private ArrayPrototype(Engine engine) : base(engine)
        {
        }

        public static ArrayPrototype CreatePrototypeObject(Engine engine, ArrayConstructor arrayConstructor)
        {
            var obj = new ArrayPrototype(engine) { Extensible = true };

            obj.FastAddProperty("constructor", arrayConstructor, false, false, false);

            return obj;
        }

        public void Configure()
        {
            // Array prototype functions
            FastAddProperty("push", new ClrFunctionInstance<ArrayInstance, object>(Engine, Push), false, false, false);
            FastAddProperty("pop", new ClrFunctionInstance<ArrayInstance, object>(Engine, Pop), false, false, false);
        }
            

        public object Push(object thisObject, object[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObject);
            var lenVal = o.Get("length");
            var n = TypeConverter.ToUint32(lenVal);
            foreach (var e in arguments)
            {
                o.Put(TypeConverter.ToString(n), e, true);
                n++;
            }
            o.Put("length", n, true);

            return n;
        }

        public object Pop(object thisObject, object[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObject);
            var lenVal = o.Get("length");
            var len = TypeConverter.ToUint32(lenVal);
            if (len == 0)
            {
                o.Put("length", 0, true);
                return Undefined.Instance;
            }
            else
            {
                var indx = TypeConverter.ToString(len - 1);
                var element = o.Get(indx);
                o.Delete(indx, true);
                o.Put("length", indx, true);
                return element;
            }
        }
    }
}
