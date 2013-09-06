using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Array
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.4.4
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
            FastAddProperty("slice", new ClrFunctionInstance<object, object>(Engine, Slice, 2), true, false, true);
            FastAddProperty("sort", new ClrFunctionInstance<object, ObjectInstance>(Engine, Sort, 1), true, false, true);
            FastAddProperty("splice", new ClrFunctionInstance<object, ObjectInstance>(Engine, Splice, 2), true, false, true);
            FastAddProperty("unshift", new ClrFunctionInstance<object, uint>(Engine, Unshift, 1), true, false, true);
            FastAddProperty("indexOf", new ClrFunctionInstance<object, int>(Engine, IndexOf, 1), true, false, true);
            FastAddProperty("lastIndexOf", new ClrFunctionInstance<object, int>(Engine, LastIndexOf, 1), true, false, true);
            FastAddProperty("every", new ClrFunctionInstance<object, bool>(Engine, Every, 1), true, false, true);
            FastAddProperty("some", new ClrFunctionInstance<object, bool>(Engine, Some, 1), true, false, true);
            FastAddProperty("forEach", new ClrFunctionInstance<object, object>(Engine, ForEach, 1), true, false, true);
            FastAddProperty("map", new ClrFunctionInstance<ArrayInstance, object>(Engine, Map), true, false, true);
            FastAddProperty("filter", new ClrFunctionInstance<ArrayInstance, object>(Engine, Filter), true, false, true);
            FastAddProperty("reduce", new ClrFunctionInstance<ArrayInstance, object>(Engine, Reduce), true, false, true);
            FastAddProperty("reduceRight", new ClrFunctionInstance<ArrayInstance, object>(Engine, ReduceRight), true, false, true);
        }

        private int LastIndexOf(object thisObj, object[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);
            if (len == 0)
            {
                return -1;
            }

            var n = arguments.Length > 1 ? (int)TypeConverter.ToInteger(arguments[1]) : (int)len - 1;
            int k;
            if (n >= 0)
            {
                k = System.Math.Min(n, (int)len - 1); // min
            }
            else
            {
                k = (int)len - System.Math.Abs(n);
            }
            var searchElement = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            for (; k > 0; k--)
            {
                var kString = TypeConverter.ToString(k);
                var kPresent = o.HasProperty(kString);
                if (kPresent)
                {
                    var elementK = o.Get(kString);
                    var same = ExpressionInterpreter.StriclyEqual(elementK, searchElement);
                    if (same)
                    {
                        return k;
                    }
                }
            }
            return -1;
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
            var callbackfn = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            var thisArg = arguments.Length > 1 ? arguments[1] : Undefined.Instance;

            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);

            var callable = callbackfn as ICallable;
            if (callable == null)
            {
                throw new JavaScriptException(Engine.TypeError, "Argument must be callable");
            }

            for (var k = 0; k < len; k++)
            {
                var pk = k.ToString();
                var kpresent = o.HasProperty(pk);
                if (kpresent)
                {
                    var kvalue = o.Get(pk);
                    var testResult = callable.Call(thisArg, new object[] { kvalue, k, o });
                }
            }

            return Undefined.Instance;
        }

        private object Some(ArrayInstance arg1, object[] arg2)
        {
            var callbackfn = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            var thisArg = arguments.Length > 1 ? arguments[1] : Undefined.Instance;

            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);

            var callable = callbackfn as ICallable;
            if (callable == null)
            {
                throw new JavaScriptException(Engine.TypeError, "Argument must be callable");
            }

            for (var k = 0; k < len; k++)
            {
                var pk = k.ToString();
                var kpresent = o.HasProperty(pk);
                if (kpresent)
                {
                    var kvalue = o.Get(pk);
                    var testResult = callable.Call(thisArg, new object[] { kvalue, k, o });
                    if (TypeConverter.ToBoolean(testResult) == true)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool Every(object thisObj, object[] arguments)
        {
            var callbackfn = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            var thisArg = arguments.Length > 1 ? arguments[1] : Undefined.Instance;

            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);

            var callable = callbackfn as ICallable;
            if (callable == null)
            {
                throw new JavaScriptException(Engine.TypeError, "Argument must be callable");
            }

            for (var k = 0; k < len; k++)
            {
                var pk = k.ToString();
                var kpresent = o.HasProperty(pk);
                if (kpresent)
                {
                    var kvalue = o.Get(pk);
                    var testResult = callable.Call(thisArg, new object[] { kvalue, k, o });
                    if (!TypeConverter.ToBoolean(testResult))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private int IndexOf(object thisObj, object[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);
            if (len == 0)
            {
                return -1;
            }

            var n = arguments.Length > 1 ? (int)TypeConverter.ToInteger(arguments[1]) : 0;
            if (n >= len)
            {
                return -1;
            }
            int k;
            if (n >= 0)
            {
                k = n;
            }
            else
            {
                k = (int)len - System.Math.Abs(n);
                if (k < 0)
                {
                    k = 0;
                }
            }
            var searchElement = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            for (; k < len; k++)
            {
                var kString = TypeConverter.ToString(k);
                var kPresent = o.HasProperty(kString);
                if (kPresent)
                {
                    var elementK = o.Get(kString);
                    var same = ExpressionInterpreter.StriclyEqual(elementK, searchElement);
                    if (same)
                    {
                        return k;
                    }
                }
            }
            return -1;
        }

        private ObjectInstance Splice(object thisObj, object[] arguments)
        {
            var start = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            var deleteCount = arguments.Length > 1 ? arguments[1] : Undefined.Instance;

            var o = TypeConverter.ToObject(Engine, thisObj);
            var a = Engine.Array.Construct(Arguments.Empty);
            var lenVal = o.Get("length");
            var len = TypeConverter.ToUint32(lenVal);
            var relativeStart = TypeConverter.ToInteger(start);

            uint actualStart;
            if (relativeStart < 0)
            {
                actualStart = (uint)System.Math.Max(len + relativeStart, 0);
            }
            else
            {
                actualStart = (uint)System.Math.Min(relativeStart, len);
            }

            var actualDeleteCount = System.Math.Min(System.Math.Max(TypeConverter.ToInteger(deleteCount), 0), len - actualStart);
            for (var k = 0; k < actualDeleteCount; k++)
            {
                var from = (actualStart + k).ToString();
                var fromPresent = o.HasProperty(from);
                if (fromPresent)
                {
                    var fromValue = o.Get(from); 
                    a.DefineOwnProperty(k.ToString(), new DataDescriptor(fromValue) { Writable = true, Enumerable = true, Configurable = true }, false);
                }
            }
            
            var items = arguments.Skip(2).ToArray();
            if (items.Length < actualDeleteCount)
            {
                for (var k = actualStart; k < len - actualDeleteCount; k++)
                {
                    var from = (k + actualDeleteCount).ToString();
                    var to = (k + items.Length).ToString();
                    var fromPresent = o.HasProperty(from);
                    if (fromPresent)
                    {
                        var fromValue = o.Get(from);
                        o.Put(to, fromValue, true);
                    }
                    else
                    {
                        o.Delete(to, true);
                    }
                }
                for (var k = len; k > len - actualDeleteCount + items.Length; k-- )
                {
                    o.Delete((k - 1).ToString(), true);
                }
            }
            else if (items.Length > actualDeleteCount)
            {
                for (var k = len - actualDeleteCount; k > actualStart; k--)
                {
                    var from = (k + actualDeleteCount - 1).ToString();
                    var to = (k + items.Length - 1).ToString();
                    var fromPresent = o.HasProperty(from);
                    if (fromPresent)
                    {
                        var fromValue = o.Get(from);
                        o.Put(to, fromValue, true);
                    }
                    else
                    {
                        o.Delete(to, true);
                    }
                }
            }

            for(var k = 0; k< items.Length; k++)
            {
                var e = items[k];
                o.Put((k+actualStart).ToString(), e, true);
            }

            o.Put("length", len - actualDeleteCount + items.Length, true);
            return a;
        }

        private uint Unshift(object thisObj, object[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenVal = o.Get("length");
            var len = TypeConverter.ToUint32(lenVal);
            var argCount = (uint)arguments.Length;
            for (var k = len; k > 0; k--)
            {
                var from = (k - 1).ToString();
                var to = (k + argCount - 1).ToString();
                var fromPresent = o.HasProperty(from);
                if (fromPresent)
                {
                    var fromValue = o.Get(from);
                    o.Put(to, fromValue, true);
                }
                else
                {
                    o.Delete(to, true);
                }
            }
            for (var j = 0; j < argCount; j++)
            {
                o.Put(j.ToString(), arguments[j], true);
            }
            o.Put("length", len + argCount, true);
            return len + argCount;
        }

        private ObjectInstance Sort(object thisObj, object[] arguments)
        {
            var obj = thisObj as ObjectInstance;

            if(obj == null)
            {
                throw new JavaScriptException(Engine.TypeError, "Array.prorotype.sort can only be applied on objects");  
            }

            var len = obj.Get("length");
            var lenVal = TypeConverter.ToInt32(len);
            if (lenVal <= 1)
            {
                return obj;
            }

            var compareArg = arguments.Length > 0 ? arguments[0] : Undefined.Instance;

            if (compareArg != Undefined.Instance && !(compareArg is ICallable))
            {
                throw new JavaScriptException(Engine.TypeError, "The sort argument must be a function");
            }

            var compareFn = compareArg as ICallable;

            Comparison<object> comparer = (x, y) =>
                {
                    if (x == Undefined.Instance && y == Undefined.Instance)
                    {
                        return 0;
                    }

                    if (x == Undefined.Instance)
                    {
                        return 1;
                    }

                    if (y == Undefined.Instance)
                    {
                        return -1;
                    }

                    if (compareFn != null)
                    {
                        var s = (int) TypeConverter.ToUint32(compareFn.Call(Undefined.Instance, new[] {x, y}));
                        return s;
                    }

                    var xString = TypeConverter.ToString(x);
                    var yString = TypeConverter.ToString(y);

                    var r = System.String.CompareOrdinal(xString, yString);
                    return r;
                };

            var array = Enumerable.Range(0, lenVal).Select(i => obj.Get(i.ToString())).ToArray();
            
            // don't eat inner exceptions
            try
            {
                System.Array.Sort(array, comparer);
            }
            catch (InvalidOperationException e)
            {
                throw e.InnerException;
            }

            foreach (var i in Enumerable.Range(0, lenVal))
            {
                obj.Put(i.ToString(), array[i], false);
            }

            return obj;
        }

        private object Slice(object thisObj, object[] arguments)
        {
            var start = arguments.Length > 0 ? arguments[0] : Undefined.Instance;
            var end = arguments.Length > 1 ? arguments[1] : Undefined.Instance;

            var o = TypeConverter.ToObject(Engine, thisObj);
            var a = Engine.Array.Construct(Arguments.Empty);
            var lenVal = o.Get("length");
            var len = TypeConverter.ToUint32(lenVal);

            var relativeStart = TypeConverter.ToInteger(start);
            uint k;
            if (relativeStart < 0)
            {
                k = (uint)System.Math.Max(len + relativeStart, 0);
            }
            else
            {
                k = (uint)System.Math.Min(TypeConverter.ToInteger(start), len);
            }

            uint final;
            if (end == Undefined.Instance)
            {
                final = TypeConverter.ToUint32(len);
            }
            else
            {
                double relativeEnd = TypeConverter.ToInteger(end);
                if (relativeEnd < 0)
                {
                    final = (uint)System.Math.Max(len + relativeEnd, 0);
                }
                else
                {
                    final = (uint)System.Math.Min(TypeConverter.ToInteger(relativeEnd), len);
                }
            }

            var n = 0;
            for (; k < final; k++)
            {
                var pk = TypeConverter.ToString(k);
                var kPresent = o.HasProperty(pk);
                if (kPresent)
                {
                    var kValue = o.Get(pk);
                    a.DefineOwnProperty(TypeConverter.ToString(n),
                                        new DataDescriptor(kValue)
                                            {
                                                Writable = true,
                                                Enumerable = true,
                                                Configurable = true
                                            }, false);
                }
                n++;
            }

            return a;
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
            var items = new List<object> {o};
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