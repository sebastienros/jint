using System;
using System.Collections.Generic;
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
                var kString = k.ToString();
                if (o.TryGetValue(kString, out var value))
                {
                    var same = ExpressionInterpreter.StrictlyEqual(value, searchElement);
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
                    var pk = TypeConverter.ToString(k);
                    if (kPresent = o.TryGetValue(pk, out var temp))
                    {
                        accumulator = temp;
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
                var pk = TypeConverter.ToString(k);
                if (o.TryGetValue(pk, out var kvalue))
                {
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
            var jsValues = new JsValue[3];
            for (var k = 0; k < len; k++)
            {
                var pk = TypeConverter.ToString(k);
                if (o.TryGetValue(pk, out var kvalue))
                {
                    jsValues[0] = kvalue;
                    jsValues[1] = k;
                    jsValues[2] = o;
                    var selected = callable.Call(thisArg, jsValues);
                    if (TypeConverter.ToBoolean(selected))
                    {
                        a.DefineOwnProperty(TypeConverter.ToString(to), new PropertyDescriptor(kvalue, true, true, true), false);
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

            var a = Engine.Array.Construct(new JsValue[] {len}, len);
            var jsValues = new JsValue[3];
            for (var k = 0; k < len; k++)
            {
                var pk = TypeConverter.ToString(k);
                if (o.TryGetValue(pk, out var kvalue))
                {
                    jsValues[0] = kvalue;
                    jsValues[1] = k;
                    jsValues[2] = o;
                    var mappedValue = callable.Call(thisArg, jsValues);
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

            var jsValues = new JsValue[3];
            for (var k = 0; k < len; k++)
            {
                var pk = TypeConverter.ToString(k);
                if (o.TryGetValue(pk, out var kvalue))
                {
                    jsValues[0] = kvalue;
                    jsValues[1] = k;
                    jsValues[2] = o;
                    callable.Call(thisArg, jsValues);
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

            var jsValues = new JsValue[3];
            for (var k = 0; k < len; k++)
            {
                var pk = TypeConverter.ToString(k);
                if (o.TryGetValue(pk, out var kvalue))
                {
                    jsValues[0] = kvalue;
                    jsValues[1] = k;
                    jsValues[2] = o;
                    var testResult = callable.Call(thisArg, jsValues);
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

            var jsValues = new JsValue[3];
            for (var k = 0; k < len; k++)
            {
                var pk = TypeConverter.ToString(k);
                if (o.TryGetValue(pk, out var kvalue))
                {
                    jsValues[0] = kvalue;
                    jsValues[1] = k;
                    jsValues[2] = o;
                    var testResult = callable.Call(thisArg, jsValues);
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
                var kString = k.ToString();
                if (o.TryGetValue(kString, out var elementK))
                {
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
                var from = TypeConverter.ToString(actualStart + k);
                if (o.TryGetValue(from, out var fromValue))
                {
                    a.DefineOwnProperty(TypeConverter.ToString(k), new PropertyDescriptor(fromValue, true, true, true), false);
                }
            }

            var items = System.Array.Empty<JsValue>();
            if (arguments.Length > 2)
            {
                items = new JsValue[arguments.Length - 2];
                System.Array.Copy(arguments, 2, items, 0, items.Length);
            }
            if (items.Length < actualDeleteCount)
            {
                for (var k = actualStart; k < len - actualDeleteCount; k++)
                {
                    var from = TypeConverter.ToString(k + actualDeleteCount);
                    var to = TypeConverter.ToString(k + items.Length);
                    if (o.TryGetValue(from, out var fromValue))
                    {
                        o.Put(to, fromValue, true);
                    }
                    else
                    {
                        o.Delete(to, true);
                    }
                }
                for (var k = len; k > len - actualDeleteCount + items.Length; k-- )
                {
                    o.Delete(TypeConverter.ToString(k - 1), true);
                }
            }
            else if (items.Length > actualDeleteCount)
            {
                for (var k = len - actualDeleteCount; k > actualStart; k--)
                {
                    var from = TypeConverter.ToString(k + actualDeleteCount - 1);
                    var to = TypeConverter.ToString(k + items.Length - 1);
                    if (o.TryGetValue(from, out var fromValue))
                    {
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
                o.Put(TypeConverter.ToString(k+actualStart), e, true);
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
                var from = TypeConverter.ToString(k - 1);
                var to = TypeConverter.ToString(k + argCount - 1);
                if (o.TryGetValue(from, out var fromValue))
                {
                    o.Put(to, fromValue, true);
                }
                else
                {
                    o.Delete(to, true);
                }
            }
            for (var j = 0; j < argCount; j++)
            {
                o.Put(TypeConverter.ToString(j), arguments[j], true);
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

            int Comparer(JsValue x, JsValue y)
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
            }

            var array = new JsValue[lenVal];
            for (int i = 0; i < lenVal; ++i)
            {
                array[i] = obj.Get(TypeConverter.ToString(i));
            }

            // don't eat inner exceptions
            try
            {
                System.Array.Sort(array, Comparer);
            }
            catch (InvalidOperationException e)
            {
                throw e.InnerException;
            }

            for (var i = 0; i < lenVal; ++i)
            {
                obj.Put(TypeConverter.ToString(i), array[i], false);
            }

            return obj;
        }

        private JsValue Slice(JsValue thisObj, JsValue[] arguments)
        {
            var start = arguments.At(0);
            var end = arguments.At(1);

            var o = TypeConverter.ToObject(Engine, thisObj);
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

            var a = Engine.Array.Construct(final - k);
            var n = 0;
            for (; k < final; k++)
            {
                var pk = TypeConverter.ToString(k);
                if (o.TryGetValue(pk, out var kValue))
                {
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
                if (o.TryGetValue(from, out var fromVal))
                {
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
            var middle = (uint)System.Math.Floor(len/2.0);
            uint lower = 0;
            while (lower != middle)
            {
                var upper = len - lower - 1;
                var upperP = TypeConverter.ToString(upper);
                var lowerP = TypeConverter.ToString(lower);
                var lowerExists = o.TryGetValue(lowerP, out var lowerValue);
                var upperExists = o.TryGetValue(upperP, out var upperValue);
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
                var element = o.Get(TypeConverter.ToString(k));
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
                var nextElement = array.Get(TypeConverter.ToString(k));
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
            var items = new List<JsValue>(arguments.Length + 1) {o};
            items.AddRange(arguments);

            foreach (var e in items)
            {
                var eArray = e.TryCast<ArrayInstance>();
                if (eArray != null)
                {
                    var len =  TypeConverter.ToUint32(eArray.Get("length"));
                    for (var k = 0; k < len; k++)
                    {
                        var p = TypeConverter.ToString(k);
                        if (eArray.TryGetValue(p, out var subElement))
                        {
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
                    var pk = TypeConverter.ToString(k);
                    kPresent = o.TryGetValue(pk, out var temp);
                    if (kPresent)
                    {
                        accumulator = temp;
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
                var pk = TypeConverter.ToString(k);
                if (o.TryGetValue(pk, out var kvalue))
                {
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
            for (var i = 0; i < arguments.Length; i++)
            {
                JsValue e = arguments[i];
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