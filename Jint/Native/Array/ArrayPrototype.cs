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
            obj.FastAddProperty("constructor", arrayConstructor, true, false, true);

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance(Engine, ToString, 0), true, false, true);
            FastAddProperty("toLocaleString", new ClrFunctionInstance(Engine, ToLocaleString), true, false, true);
            FastAddProperty("concat", new ClrFunctionInstance(Engine, Concat, 1), true, false, true);
            FastAddProperty("join", new ClrFunctionInstance(Engine, Join, 1), true, false, true);
            FastAddProperty("pop", new ClrFunctionInstance(Engine, Pop), true, false, true);
            FastAddProperty("push", new ClrFunctionInstance(Engine, Push, 1), true, false, true);
            FastAddProperty("reverse", new ClrFunctionInstance(Engine, Reverse), true, false, true);
            FastAddProperty("shift", new ClrFunctionInstance(Engine, Shift), true, false, true);
            FastAddProperty("slice", new ClrFunctionInstance(Engine, Slice, 2), true, false, true);
            FastAddProperty("sort", new ClrFunctionInstance(Engine, Sort, 1), true, false, true);
            FastAddProperty("splice", new ClrFunctionInstance(Engine, Splice, 2), true, false, true);
            FastAddProperty("unshift", new ClrFunctionInstance(Engine, Unshift, 1), true, false, true);
            FastAddProperty("indexOf", new ClrFunctionInstance(Engine, IndexOf, 1), true, false, true);
            FastAddProperty("lastIndexOf", new ClrFunctionInstance(Engine, LastIndexOf, 1), true, false, true);
            FastAddProperty("every", new ClrFunctionInstance(Engine, Every, 1), true, false, true);
            FastAddProperty("some", new ClrFunctionInstance(Engine, Some, 1), true, false, true);
            FastAddProperty("forEach", new ClrFunctionInstance(Engine, ForEach, 1), true, false, true);
            FastAddProperty("map", new ClrFunctionInstance(Engine, Map, 1), true, false, true);
            FastAddProperty("filter", new ClrFunctionInstance(Engine, Filter, 1), true, false, true);
            FastAddProperty("reduce", new ClrFunctionInstance(Engine, Reduce, 1), true, false, true);
            FastAddProperty("reduceRight", new ClrFunctionInstance(Engine, ReduceRight, 1), true, false, true);
        }

        private JsValue LastIndexOf(JsValue thisObj, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);
            if (len == 0)
            {
                return -1;
            }

            var n = arguments.Length > 1 ? TypeConverter.ToInteger(arguments[1]) : len - 1;
            double k;
            if (n >= 0)
            {
                k = System.Math.Min(n, len - 1); // min
            }
            else
            {
                k = len - System.Math.Abs(n);
            }
            var searchElement = arguments.At(0);
            for (; k >= 0; k--)
            {
                var kString = TypeConverter.ToString(k);
                var kPresent = o.HasProperty(kString);
                if (kPresent)
                {
                    var elementK = o.Get(kString);
                    var same = ExpressionInterpreter.StrictlyEqual(elementK, searchElement);
                    if (same)
                    {
                        return k;
                    }
                }
            }
            return -1;
        }

        private JsValue Reduce(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var initialValue = arguments.At(1);

            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);

            var callable = callbackfn.TryCast<ICallable>(x =>
            {
                throw new JavaScriptException(Engine.TypeError, "Argument must be callable");
            });

            if (len == 0 && arguments.Length < 2)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var k = 0;
            JsValue accumulator = Undefined.Instance;
            if (arguments.Length > 1)
            {
                accumulator = initialValue;
            }
            else
            {
                var kPresent = false;
                while (kPresent == false && k < len)
                {
                    var pk = k.ToString();
                    kPresent = o.HasProperty(pk);
                    if (kPresent)
                    {
                        accumulator = o.Get(pk);
                    }
                    k++;
                }
                if (kPresent == false)
                {
                    throw new JavaScriptException(Engine.TypeError);
                }
            }

            while(k < len)
            {
                var pk = k.ToString();
                var kPresent = o.HasProperty(pk);
                if (kPresent)
                {
                    var kvalue = o.Get(pk);
                    accumulator = callable.Call(Undefined.Instance, new [] { accumulator, kvalue, k, o });
                }
                k++;
            }

            return accumulator;
        }

        private JsValue Filter(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);

            var callable = callbackfn.TryCast<ICallable>(x =>
            {
                throw new JavaScriptException(Engine.TypeError,
                    "Argument must be callable");
            });

            var a = (ArrayInstance)Engine.Array.Construct(Arguments.Empty);

            var to = 0;
            for (var k = 0; k < len; k++)
            {
                var pk = k.ToString();
                var kpresent = o.HasProperty(pk);
                if (kpresent)
                {
                    var kvalue = o.Get(pk);
                    var selected = callable.Call(thisArg, new [] { kvalue, k, o });
                    if (TypeConverter.ToBoolean(selected))
                    {
                        a.DefineOwnProperty(to.ToString(), new PropertyDescriptor(kvalue, true, true, true), false);
                        to++;
                    }
                }
            }

            return a;
        }

        private JsValue Map(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);

            var callable = callbackfn.TryCast<ICallable>(x =>
            {
                throw new JavaScriptException(Engine.TypeError, "Argument must be callable");
            });

            var a = Engine.Array.Construct(new JsValue[] {len});

            for (var k = 0; k < len; k++)
            {
                var pk = k.ToString();
                var kpresent = o.HasProperty(pk);
                if (kpresent)
                {
                    var kvalue = o.Get(pk);
                    var mappedValue = callable.Call(thisArg, new [] { kvalue, k, o });
                    a.DefineOwnProperty(pk, new PropertyDescriptor(mappedValue, true, true, true), false);
                }
            }

            return a;
        }

        private JsValue ForEach(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);

            var callable = callbackfn.TryCast<ICallable>(x =>
            {
                throw new JavaScriptException(Engine.TypeError, "Argument must be callable");
            });

            for (var k = 0; k < len; k++)
            {
                var pk = k.ToString();
                var kpresent = o.HasProperty(pk);
                if (kpresent)
                {
                    var kvalue = o.Get(pk);
                    callable.Call(thisArg, new [] { kvalue, k, o });
                }
            }

            return Undefined.Instance;
        }

        private JsValue Some(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);

            var callable = callbackfn.TryCast<ICallable>(x =>
            {
                throw new JavaScriptException(Engine.TypeError, "Argument must be callable");
            });

            for (var k = 0; k < len; k++)
            {
                var pk = k.ToString();
                var kpresent = o.HasProperty(pk);
                if (kpresent)
                {
                    var kvalue = o.Get(pk);
                    var testResult = callable.Call(thisArg, new [] { kvalue, k, o });
                    if (TypeConverter.ToBoolean(testResult))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private JsValue Every(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);

            var callable = callbackfn.TryCast<ICallable>(x =>
            {
                throw new JavaScriptException(Engine.TypeError, "Argument must be callable");
            });

            for (var k = 0; k < len; k++)
            {
                var pk = k.ToString();
                var kpresent = o.HasProperty(pk);
                if (kpresent)
                {
                    var kvalue = o.Get(pk);
                    var testResult = callable.Call(thisArg, new [] { kvalue, k, o });
                    if (false == TypeConverter.ToBoolean(testResult))
                    {
                        return JsValue.False;
                    }
                }
            }

            return JsValue.True;
        }

        private JsValue IndexOf(JsValue thisObj, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);
            if (len == 0)
            {
                return -1;
            }

            var n = arguments.Length > 1 ? TypeConverter.ToInteger(arguments[1]) : 0;
            if (n >= len)
            {
                return -1;
            }
            double k;
            if (n >= 0)
            {
                k = n;
            }
            else
            {
                k = len - System.Math.Abs(n);
                if (k < 0)
                {
                    k = 0;
                }
            }
            var searchElement = arguments.At(0);
            for (; k < len; k++)
            {
                var kString = TypeConverter.ToString(k);
                var kPresent = o.HasProperty(kString);
                if (kPresent)
                {
                    var elementK = o.Get(kString);
                    var same = ExpressionInterpreter.StrictlyEqual(elementK, searchElement);
                    if (same)
                    {
                        return k;
                    }
                }
            }
            return -1;
        }

        private JsValue Splice(JsValue thisObj, JsValue[] arguments)
        {
            var start = arguments.At(0);
            var deleteCount = arguments.At(1);

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
                    a.DefineOwnProperty(k.ToString(), new PropertyDescriptor(fromValue, true, true, true), false);
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

        private JsValue Unshift(JsValue thisObj, JsValue[] arguments)
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

        private JsValue Sort(JsValue thisObj, JsValue[] arguments)
        {
            if (!thisObj.IsObject())
            {
                throw new JavaScriptException(Engine.TypeError, "Array.prorotype.sort can only be applied on objects");  
            }

            var obj = thisObj.AsObject();

            var len = obj.Get("length");
            var lenVal = TypeConverter.ToInt32(len);
            if (lenVal <= 1)
            {
                return obj;
            }

            var compareArg = arguments.At(0);
            ICallable compareFn = null;
            if (compareArg != Undefined.Instance)
            {
                compareFn = compareArg.TryCast<ICallable>(x =>
                {
                    throw new JavaScriptException(Engine.TypeError, "The sort argument must be a function");
                });
            }

            Comparison<JsValue> comparer = (x, y) =>
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
                        var s = TypeConverter.ToNumber(compareFn.Call(Undefined.Instance, new[] {x, y}));
                        if (s < 0)
                        {
                            return -1;
                        }
                        if (s > 0)
                        {
                            return 1;
                        }
                        return 0;
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

        private JsValue Slice(JsValue thisObj, JsValue[] arguments)
        {
            var start = arguments.At(0);
            var end = arguments.At(1);

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
                                        new PropertyDescriptor(kValue, true, true, true), false);
                }
                n++;
            }

            return a;
        }

        private JsValue Shift(JsValue thisObj, JsValue[] arg2)
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

        private JsValue Reverse(JsValue thisObj, JsValue[] arguments)
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

        private JsValue Join(JsValue thisObj, JsValue[] arguments)
        {
            var separator = arguments.At(0);
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

        private JsValue ToLocaleString(JsValue thisObj, JsValue[] arguments)
        {
            var array = TypeConverter.ToObject(Engine, thisObj);
            var arrayLen = array.Get("length");
            var len = TypeConverter.ToUint32(arrayLen);
            const string separator = ",";
            if (len == 0)
            {
                return "";
            }
            JsValue r;
            var firstElement = array.Get("0");
            if (firstElement == Null.Instance || firstElement == Undefined.Instance)
            {
                r = "";
            }
            else
            {
                var elementObj = TypeConverter.ToObject(Engine, firstElement);
                var func = elementObj.Get("toLocaleString").TryCast<ICallable>(x =>
                {
                    throw new JavaScriptException(Engine.TypeError);
                });

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
                    var func = elementObj.Get("toLocaleString").TryCast<ICallable>(x =>
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    });
                    r = func.Call(elementObj, Arguments.Empty);
                }
                r = s + r;
            }

            return r;

        }

        private JsValue Concat(JsValue thisObj, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObj);
            var a = Engine.Array.Construct(Arguments.Empty);
            var n = 0;
            var items = new List<JsValue> {o};
            items.AddRange(arguments);

            foreach (var e in items)
            {
                var eArray = e.TryCast<ArrayInstance>();
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
                            a.DefineOwnProperty(TypeConverter.ToString(n), new PropertyDescriptor(subElement, true, true, true), false);
                        }
                        n++;
                    }
                }
                else
                {
                    a.DefineOwnProperty(TypeConverter.ToString(n), new PropertyDescriptor(e, true, true, true ), false);
                    n++;
                }
            }

            // this is not in the specs, but is necessary in case the last element of the last
            // array doesn't exist, and thus the length would not be incremented
            a.DefineOwnProperty("length", new PropertyDescriptor(n, null, null, null), false);
            
            return a;
        }

        private JsValue ToString(JsValue thisObj, JsValue[] arguments)
        {
            var array = TypeConverter.ToObject(Engine, thisObj);
            ICallable func;
            func = array.Get("join").TryCast<ICallable>(x =>
            {
                func = Engine.Object.PrototypeObject.Get("toString").TryCast<ICallable>(y =>
                {
                    throw new ArgumentException();
                });
            });

            return func.Call(array, Arguments.Empty);
        }

        private JsValue ReduceRight(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var initialValue = arguments.At(1);

            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);

            var callable = callbackfn.TryCast<ICallable>(x =>
            {
                throw new JavaScriptException(Engine.TypeError, "Argument must be callable");
            });

            if (len == 0 && arguments.Length < 2)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            int k = (int)len - 1;
            JsValue accumulator = Undefined.Instance;
            if (arguments.Length > 1)
            {
                accumulator = initialValue;
            }
            else
            {
                var kPresent = false;
                while (kPresent == false && k >= 0)
                {
                    var pk = k.ToString();
                    kPresent = o.HasProperty(pk);
                    if (kPresent)
                    {
                        accumulator = o.Get(pk);
                    }
                    k--;
                }
                if (kPresent == false)
                {
                    throw new JavaScriptException(Engine.TypeError);
                }
            }

            for (; k >= 0; k--)
            {
                var pk = k.ToString();
                var kPresent = o.HasProperty(pk);
                if (kPresent)
                {
                    var kvalue = o.Get(pk);
                    accumulator = callable.Call(Undefined.Instance, new [] { accumulator, kvalue, k, o });
                }
            }

            return accumulator;
        }

        public JsValue Push(JsValue thisObject, JsValue[] arguments)
        {
            ObjectInstance o = TypeConverter.ToObject(Engine, thisObject);
            var lenVal = TypeConverter.ToNumber(o.Get("length"));
            
            // cast to double as we need to prevent an overflow
            double n = TypeConverter.ToUint32(lenVal);
            foreach (JsValue e in arguments)
            {
                o.Put(TypeConverter.ToString(n), e, true);
                n++;
            }

            o.Put("length", n, true);
            
            return n;
        }

        public JsValue Pop(JsValue thisObject, JsValue[] arguments)
        {
            ObjectInstance o = TypeConverter.ToObject(Engine, thisObject);
            var lenVal = TypeConverter.ToNumber(o.Get("length"));

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
                JsValue element = o.Get(indx);
                o.Delete(indx, true);
                o.Put("length", len, true);
                return element;
            }
        }
    }
}