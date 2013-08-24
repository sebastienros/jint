using System;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Array
{
    /// <summary>
    ///     http://www.ecma-international.org/ecma-262/5.1/#sec-15.4.4
    /// </summary>
    public sealed class ArrayPrototype : ArrayInstance
    {
        private ArrayPrototype(Engine engine) : base(engine)
        {
        }

        public static ArrayPrototype CreatePrototypeObject(Engine engine, ArrayConstructor arrayConstructor)
        {
            var obj = new ArrayPrototype(engine)
                {
                    Extensible = true, 
                    Prototype = engine.Object.PrototypeObject
                };

            obj.FastAddProperty("length", 0, false, false, false);
            obj.FastAddProperty("constructor", arrayConstructor, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance<object, object>(Engine, ToString), false, false, false);
            FastAddProperty("toLocaleString", new ClrFunctionInstance<object, object>(Engine, ToLocaleString), false, false, false);
            FastAddProperty("concat", new ClrFunctionInstance<object, object>(Engine, Concat), false, false, false);
            FastAddProperty("join", new ClrFunctionInstance<object, object>(Engine, Join), false, false, false);
            FastAddProperty("pop", new ClrFunctionInstance<ArrayInstance, object>(Engine, Pop), false, false, false);
            FastAddProperty("push", new ClrFunctionInstance<ArrayInstance, object>(Engine, Push), false, false, false);
            FastAddProperty("reverse", new ClrFunctionInstance<object, object>(Engine, Reverse), false, false, false);
            FastAddProperty("shift", new ClrFunctionInstance<ArrayInstance, object>(Engine, Shift), false, false, false);
            FastAddProperty("slice", new ClrFunctionInstance<ArrayInstance, object>(Engine, Slice), false, false, false);
            FastAddProperty("sort", new ClrFunctionInstance<ArrayInstance, object>(Engine, Sort), false, false, false);
            FastAddProperty("splice", new ClrFunctionInstance<ArrayInstance, object>(Engine, Splice), false, false, false);
            FastAddProperty("unshift", new ClrFunctionInstance<ArrayInstance, object>(Engine, Unshift), false, false, false);
            FastAddProperty("indexOf", new ClrFunctionInstance<ArrayInstance, object>(Engine, IndexOf), false, false, false);
            FastAddProperty("lastIndexOf", new ClrFunctionInstance<ArrayInstance, object>(Engine, LastIndexOf), false, false, false);
            FastAddProperty("every", new ClrFunctionInstance<ArrayInstance, object>(Engine, Every), false, false, false);
            FastAddProperty("some", new ClrFunctionInstance<ArrayInstance, object>(Engine, Some), false, false, false);
            FastAddProperty("forEach", new ClrFunctionInstance<ArrayInstance, object>(Engine, ForEach), false, false, false);
            FastAddProperty("map", new ClrFunctionInstance<ArrayInstance, object>(Engine, Map), false, false, false);
            FastAddProperty("filter", new ClrFunctionInstance<ArrayInstance, object>(Engine, Filter), false, false, false);
            FastAddProperty("reduce", new ClrFunctionInstance<ArrayInstance, object>(Engine, Reduce), false, false, false);
            FastAddProperty("reduceRight", new ClrFunctionInstance<ArrayInstance, object>(Engine, ReduceRight), false, false, false);
        }

        private object LastIndexOf(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        private object Reduce(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        private object Filter(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        private object Map(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        private object ForEach(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        private object Some(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        private object Every(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        private object IndexOf(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        private object Splice(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        private object Unshift(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        private object Sort(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        private object Slice(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        private object Shift(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        private object Reverse(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private object Join(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private object ToLocaleString(object thisObj, object[] arguments)
        {
            var array = TypeConverter.ToObject(Engine, thisObj);
            var arrayLen = array.Get("length");
            var len = TypeConverter.ToUint32(arrayLen);
            var separator = ",";
            if (len == 0)
            {
                return "";
            }
            object r;
            var firstElement = array.Get("0");
            if (firstElement == Null.Instance || firstElement == Undefined.Instance)
            {
                r = "";
            }
            else
            {
                var elementObj = TypeConverter.ToObject(Engine, firstElement);
                var func = elementObj.Get("toLocaleString") as ICallable;
                if (func == null)
                {
                    throw new JavaScriptException(Engine.TypeError);
                }
                r = func.Call(elementObj, Arguments.Empty);
            }
            for (var k = 1; k < len; k++)
            {
                string s = r + separator;
                var nextElement = array.Get(k.ToString());
                if (nextElement == Undefined.Instance || nextElement == Null.Instance)
                {
                    r = "";
                }
                else
                {
                    var elementObj = TypeConverter.ToObject(Engine, firstElement);
                    var func = elementObj.Get("toLocaleString") as ICallable;
                    if (func == null)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }
                    r = func.Call(elementObj, Arguments.Empty);
                }
                r = s + r;
            }

            return r;

        }

        private object Concat(object thisObj, object[] arguments)
        {
            throw new NotImplementedException();
        }

        private object ToString(object thisObj, object[] arguments)
        {
            var array = TypeConverter.ToObject(Engine, thisObj);
            var func = array.Get("join") as ICallable;
            if (func == null)
            {
                func = Engine.Object.PrototypeObject.Get("toString") as ICallable;
                
                if (func == null)
                {
                    throw new ArgumentException();
                }
            }

            return func.Call(array, Arguments.Empty);
        }

        private object ReduceRight(ArrayInstance arg1, object[] arg2)
        {
            throw new NotImplementedException();
        }

        public object Push(object thisObject, object[] arguments)
        {
            ObjectInstance o = TypeConverter.ToObject(Engine, thisObject);
            object lenVal = o.Get("length");
            uint n = TypeConverter.ToUint32(lenVal);
            foreach (object e in arguments)
            {
                o.Put(TypeConverter.ToString(n), e, true);
                n++;
            }
            o.Put("length", n, true);

            return n;
        }

        public object Pop(object thisObject, object[] arguments)
        {
            ObjectInstance o = TypeConverter.ToObject(Engine, thisObject);
            object lenVal = o.Get("length");
            uint len = TypeConverter.ToUint32(lenVal);
            if (len == 0)
            {
                o.Put("length", 0, true);
                return Undefined.Instance;
            }
            else
            {
                string indx = TypeConverter.ToString(len - 1);
                object element = o.Get(indx);
                o.Delete(indx, true);
                o.Put("length", indx, true);
                return element;
            }
        }
    }
}