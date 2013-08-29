using System;
using System.Collections.Generic;
using System.Dynamic;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
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

            obj.FastAddProperty("length", 0, true, false, false);
            obj.FastAddProperty("constructor", arrayConstructor, false, false, false);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance<object, object>(Engine, ToString, 0), true, false, true);
            FastAddProperty("toLocaleString", new ClrFunctionInstance<object, object>(Engine, ToLocaleString), true, false, true);
            FastAddProperty("concat", new ClrFunctionInstance<object, object>(Engine, Concat, 1), true, false, true);
            FastAddProperty("join", new ClrFunctionInstance<object, object>(Engine, Join, 1), true, false, true);
            FastAddProperty("pop", new ClrFunctionInstance<object, object>(Engine, Pop), true, false, true);
            FastAddProperty("push", new ClrFunctionInstance<object, object>(Engine, Push, 1), true, false, true);
            FastAddProperty("reverse", new ClrFunctionInstance<object, object>(Engine, Reverse), true, false, true);
            FastAddProperty("shift", new ClrFunctionInstance<object, object>(Engine, Shift), true, false, true);
            FastAddProperty("slice", new ClrFunctionInstance<ArrayInstance, object>(Engine, Slice, 2), true, false, true);
            FastAddProperty("sort", new ClrFunctionInstance<ArrayInstance, object>(Engine, Sort), true, false, true);
            FastAddProperty("splice", new ClrFunctionInstance<ArrayInstance, object>(Engine, Splice, 2), true, false, true);
            FastAddProperty("unshift", new ClrFunctionInstance<ArrayInstance, object>(Engine, Unshift), true, false, true);
            FastAddProperty("indexOf", new ClrFunctionInstance<ArrayInstance, object>(Engine, IndexOf), true, false, true);
            FastAddProperty("lastIndexOf", new ClrFunctionInstance<ArrayInstance, object>(Engine, LastIndexOf), true, false, true);
            FastAddProperty("every", new ClrFunctionInstance<ArrayInstance, object>(Engine, Every), true, false, true);
            FastAddProperty("some", new ClrFunctionInstance<ArrayInstance, object>(Engine, Some), true, false, true);
            FastAddProperty("forEach", new ClrFunctionInstance<ArrayInstance, object>(Engine, ForEach), true, false, true);
            FastAddProperty("map", new ClrFunctionInstance<ArrayInstance, object>(Engine, Map), true, false, true);
            FastAddProperty("filter", new ClrFunctionInstance<ArrayInstance, object>(Engine, Filter), true, false, true);
            FastAddProperty("reduce", new ClrFunctionInstance<ArrayInstance, object>(Engine, Reduce), true, false, true);
            FastAddProperty("reduceRight", new ClrFunctionInstance<ArrayInstance, object>(Engine, ReduceRight), true, false, true);
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

        private object Shift(object thisObj, object[] arg2)
        {
            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenVal = o.Get("length");
            var len = TypeConverter.ToUint32(lenVal);
            if (len == 0)
            {
                o.Put("length", 0, true);
                return Undefined.Instance;
            }
            var first = o.Get("0");
            for (var k = 1; k < len; k++)
            {
                var from = TypeConverter.ToString(k);
                var to = TypeConverter.ToString(k - 1);
                var fromPresent = o.HasProperty(from);
                if (fromPresent)
                {
                    var fromVal = o.Get(from);
                    o.Put(to, fromVal, true);
                }
                else
                {
                    o.Delete(to, true);
                }
            }
            o.Delete(TypeConverter.ToString(len - 1), true);
            o.Put("length", len-1, true);

            return first;
        }

        private object Reverse(object thisObj, object[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenVal = o.Get("length");
            var len = TypeConverter.ToUint32(lenVal);
            var middle = (uint)System.Math.Floor(len/2);
            uint lower = 0;
            while (lower != middle)
            {
                var upper = len - lower - 1;
                var upperP = TypeConverter.ToString(upper);
                var lowerP = TypeConverter.ToString(lower);
                var lowerValue = o.Get(lowerP);
                var upperValue = o.Get(upperP);
                var lowerExists = o.HasProperty(lowerP);
                var upperExists = o.HasProperty(upperP);
                if (lowerExists && upperExists)
                {
                    o.Put(lowerP, upperValue, true);
                    o.Put(upperP, lowerValue, true);
                }
                if (!lowerExists && upperExists)
                {
                    o.Put(lowerP, upperValue, true);
                    o.Delete(upperP, true);
                }
                if (lowerExists && !upperExists)
                {
                    o.Delete(lowerP, true);
                    o.Put(upperP, lowerValue, true);
                }

                lower++;
            }

            return o;
        }

        private object Join(object thisObj, object[] arguments)
        {
            var separator = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenVal = o.Get("length");
            var len = TypeConverter.ToUint32(lenVal);
            if (separator == Undefined.Instance)
            {
                separator = ",";
            }
            var sep = TypeConverter.ToString(separator);
            
            // as per the spec, this has to be called after ToString(separator)
            if (len == 0)
            {
                return "";
            }

            var element0 = o.Get("0");
            string r = element0 == Undefined.Instance || element0 == Null.Instance
                                  ? ""
                                  : TypeConverter.ToString(element0);
            for (var k = 1; k < len; k++)
            {
                var s = r + sep;
                var element = o.Get(k.ToString());
                string next = element == Undefined.Instance || element == Null.Instance
                                  ? ""
                                  : TypeConverter.ToString(element);
                r = s + next;
            }
            return r;
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
                    var elementObj = TypeConverter.ToObject(Engine, nextElement);
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
            var o = TypeConverter.ToObject(Engine, thisObj);
            var a = Engine.Array.Construct(Arguments.Empty);
            var n = 0;
            var items = new List<object>() {o};
            items.AddRange(arguments);

            foreach (var e in items)
            {
                var eArray = e as ArrayInstance;
                if (eArray != null)
                {
                    var len =  TypeConverter.ToUint32(eArray.Get("length"));
                    for (var k = 0; k < len; k++)
                    {
                        var p = k.ToString();
                        var exists = eArray.HasProperty(p);
                        if (exists)
                        {
                            var subElement = eArray.Get(p);
                            a.DefineOwnProperty(TypeConverter.ToString(n), new DataDescriptor(subElement) { Writable = true, Enumerable = true, Configurable = true}, false);
                        }
                        n++;
                    }
                }
                else
                {
                    a.DefineOwnProperty(TypeConverter.ToString(n), new DataDescriptor(e) { Writable = true, Enumerable = true, Configurable = true }, false);
                    n++;
                }
            }
            return a;
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
            
            // cast to double as we need to prevent an overflow
            double n = TypeConverter.ToUint32(lenVal);
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
                len = len - 1;
                string indx = TypeConverter.ToString(len);
                object element = o.Get(indx);
                o.Delete(indx, true);
                o.Put("length", len, true);
                return element;
            }
        }
    }
}