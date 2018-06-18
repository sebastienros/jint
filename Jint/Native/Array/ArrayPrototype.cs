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
            obj.SetOwnProperty("constructor", new PropertyDescriptor(arrayConstructor, PropertyFlag.NonEnumerable));

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
            FastAddProperty("find", new ClrFunctionInstance(Engine, Find, 1), true, false, true);
            FastAddProperty("findIndex", new ClrFunctionInstance(Engine, FindIndex, 1), true, false, true);
        }

        private JsValue LastIndexOf(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();
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

            if (k < 0 || k > uint.MaxValue)
            {
                return -1;
            }

            var searchElement = arguments.At(0);
            var i = (uint) k;
            for (;; i--)
            {
                if (o.TryGetValue(i, out var value))
                {
                    var same = ExpressionInterpreter.StrictlyEqual(value, searchElement);
                    if (same)
                    {
                        return i;
                    }
                }

                if (i == 0)
                {
                    break;
                }
            }

            return -1;
        }

        private JsValue Reduce(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var initialValue = arguments.At(1);

            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();

            var callable = GetCallable(callbackfn);

            if (len == 0 && arguments.Length < 2)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var k = 0;
            JsValue accumulator = Undefined;
            if (arguments.Length > 1)
            {
                accumulator = initialValue;
            }
            else
            {
                var kPresent = false;
                while (kPresent == false && k < len)
                {
                    if (kPresent = o.TryGetValue((uint) k, out var temp))
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

            var args = new JsValue[4];
            while (k < len)
            {
                var i = (uint) k;
                if (o.TryGetValue(i, out var kvalue))
                {
                    args[0] = accumulator;
                    args[1] = kvalue;
                    args[2] = i;
                    args[3] = o.Target;
                    accumulator = callable.Call(Undefined, args);
                }

                k++;
            }

            return accumulator;
        }

        private JsValue Filter(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();

            var callable = GetCallable(callbackfn);

            var a = Engine.Array.ConstructFast(0);

            uint to = 0;
            var args = Engine.JsValueArrayPool.RentArray(3);
            for (uint k = 0; k < len; k++)
            {
                if (o.TryGetValue(k, out var kvalue))
                {
                    args[0] = kvalue;
                    args[1] = k;
                    args[2] = o.Target;
                    var selected = callable.Call(thisArg, args);
                    if (TypeConverter.ToBoolean(selected))
                    {
                        a.SetIndexValue(to, kvalue, updateLength: false);
                        to++;
                    }
                }
            }

            a.SetLength(to);
            Engine.JsValueArrayPool.ReturnArray(args);

            return a;
        }

        private JsValue Map(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj is ArrayInstance arrayInstance)
            {
                return arrayInstance.Map(arguments);
            }

            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();

            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);
            var callable = GetCallable(callbackfn);

            var jsValues = Engine.JsValueArrayPool.RentArray(1);
            jsValues[0] = len;
            var a = Engine.Array.Construct(jsValues, len);
            Engine.JsValueArrayPool.ReturnArray(jsValues);
            
            var args = Engine.JsValueArrayPool.RentArray(3);
            for (uint k = 0; k < len; k++)
            {
                if (o.TryGetValue(k, out var kvalue))
                {
                    args[0] = kvalue;
                    args[1] = k;
                    args[2] = o.Target;
                    var mappedValue = callable.Call(thisArg, args);
                    a.SetIndexValue(k, mappedValue, updateLength: false);
                }
            }

            a.SetLength(len);
            Engine.JsValueArrayPool.ReturnArray(args);

            return a;
        }

        private JsValue ForEach(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();

            var callable = GetCallable(callbackfn);

            var args = Engine.JsValueArrayPool.RentArray(3);
            for (uint k = 0; k < len; k++)
            {
                if (o.TryGetValue(k, out var kvalue))
                {
                    args[0] = kvalue;
                    args[1] = k;
                    args[2] = o.Target;
                    callable.Call(thisArg, args);
                }
            }
            Engine.JsValueArrayPool.ReturnArray(args);

            return Undefined;
        }

        private JsValue Some(JsValue thisObj, JsValue[] arguments)
        {
            var target = TypeConverter.ToObject(Engine, thisObj);
            return target.FindWithCallback(arguments, out _, out _);
        }

        private JsValue Every(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();

            var callable = GetCallable(callbackfn);

            var args = Engine.JsValueArrayPool.RentArray(3);
            for (uint k = 0; k < len; k++)
            {
                if (o.TryGetValue(k, out var kvalue))
                {
                    args[0] = kvalue;
                    args[1] = k;
                    args[2] = o.Target;
                    var testResult = callable.Call(thisArg, args);
                    if (false == TypeConverter.ToBoolean(testResult))
                    {
                        return JsBoolean.False;
                    }
                }
            }
            Engine.JsValueArrayPool.ReturnArray(args);

            return JsBoolean.True;
        }

        private JsValue IndexOf(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();
            if (len == 0)
            {
                return -1;
            }

            var startIndex = arguments.Length > 1 ? TypeConverter.ToInteger(arguments[1]) : 0;

            if (startIndex > uint.MaxValue)
            {
                return -1;
            }

            uint k;
            if (startIndex < 0)
            {
                var abs = System.Math.Abs(startIndex);
                long temp = len - (uint) abs;
                if (abs > len || temp < 0)
                {
                    temp = 0;
                }

                k = (uint) temp;
            }
            else
            {
                k = (uint) startIndex;
            }

            if (k >= len)
            {
                return -1;
            }

            uint smallestIndex = o.GetSmallestIndex();
            if (smallestIndex > k)
            {
                k = smallestIndex;
            }

            var searchElement = arguments.At(0);
            for (; k < len; k++)
            {
                if (o.TryGetValue(k, out var elementK))
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
        
        private JsValue Find(JsValue thisObj, JsValue[] arguments)
        {
            var target = TypeConverter.ToObject(Engine, thisObj);
            target.FindWithCallback(arguments, out _, out var value);
            return value;
        }
        
        private JsValue FindIndex(JsValue thisObj, JsValue[] arguments)
        {
            var target = TypeConverter.ToObject(Engine, thisObj);
            if (target.FindWithCallback(arguments, out var index, out _))
            {
                return index;
            }
            return -1;
        }

        private JsValue Splice(JsValue thisObj, JsValue[] arguments)
        {
            var start = arguments.At(0);
            var deleteCount = arguments.At(1);

            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();
            var relativeStart = TypeConverter.ToInteger(start);

            uint actualStart;
            if (relativeStart < 0)
            {
                actualStart = (uint) System.Math.Max(len + relativeStart, 0);
            }
            else
            {
                actualStart = (uint) System.Math.Min(relativeStart, len);
            }

            var actualDeleteCount = (uint) System.Math.Min(System.Math.Max(TypeConverter.ToInteger(deleteCount), 0), len - actualStart);
            var a = Engine.Array.ConstructFast(actualDeleteCount);
            for (uint k = 0; k < actualDeleteCount; k++)
            {
                if (o.TryGetValue(actualStart + k, out var fromValue))
                {
                    a.SetIndexValue(k, fromValue, updateLength: false);
                }
            }
            a.SetLength(actualDeleteCount);

            var items = System.Array.Empty<JsValue>();
            if (arguments.Length > 2)
            {
                items = new JsValue[arguments.Length - 2];
                System.Array.Copy(arguments, 2, items, 0, items.Length);
            }

            var length = len - actualDeleteCount + (uint) items.Length;
            o.EnsureCapacity(length);
            if (items.Length < actualDeleteCount)
            {
                for (uint k = actualStart; k < len - actualDeleteCount; k++)
                {
                    var from = k + actualDeleteCount;
                    var to = (uint) (k + items.Length);
                    if (o.TryGetValue(from, out var fromValue))
                    {
                        o.Put(to, fromValue, true);
                    }
                    else
                    {
                        o.DeleteAt(to);
                    }
                }

                for (var k = len; k > len - actualDeleteCount + items.Length; k--)
                {
                    o.DeleteAt(k - 1);
                }
            }
            else if (items.Length > actualDeleteCount)
            {
                for (var k = len - actualDeleteCount; k > actualStart; k--)
                {
                    var from = k + actualDeleteCount - 1;
                    uint to = (uint) (k + items.Length - 1);
                    if (o.TryGetValue(from, out var fromValue))
                    {
                        o.Put(to, fromValue, true);
                    }
                    else
                    {
                        o.DeleteAt(to);
                    }
                }
            }

            for (uint k = 0; k < items.Length; k++)
            {
                var e = items[k];
                o.Put(k + actualStart, e, true);
            }

            o.SetLength(length);
            return a;
        }

        private JsValue Unshift(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();
            var argCount = (uint) arguments.Length;
            o.EnsureCapacity(len + argCount);
            for (var k = len; k > 0; k--)
            {
                var from = k - 1;
                var to = k + argCount - 1;
                if (o.TryGetValue(from, out var fromValue))
                {
                    o.Put(to, fromValue, true);
                }
                else
                {
                    o.DeleteAt(to);
                }
            }

            for (uint j = 0; j < argCount; j++)
            {
                o.Put(j, arguments[j], true);
            }

            o.SetLength(len + argCount);
            return len + argCount;
        }

        private JsValue Sort(JsValue thisObj, JsValue[] arguments)
        {
            if (!thisObj.IsObject())
            {
                throw new JavaScriptException(Engine.TypeError, "Array.prorotype.sort can only be applied on objects");
            }

            var obj = ArrayOperations.For(thisObj.AsObject());

            var len = obj.GetLength();
            if (len <= 1)
            {
                return obj.Target;
            }

            var compareArg = arguments.At(0);
            ICallable compareFn = null;
            if (!compareArg.IsUndefined())
            {
                compareFn = compareArg.TryCast<ICallable>(x => throw new JavaScriptException(Engine.TypeError, "The sort argument must be a function"));
            }

            int Comparer(JsValue x, JsValue y)
            {
                var xUndefined = x.IsUndefined();
                var yUndefined = y.IsUndefined();
                if (xUndefined && yUndefined)
                {
                    return 0;
                }

                if (xUndefined)
                {
                    return 1;
                }

                if (yUndefined)
                {
                    return -1;
                }

                if (compareFn != null)
                {
                    var s = TypeConverter.ToNumber(compareFn.Call(Undefined, new[] {x, y}));
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

            var array = new JsValue[len];
            for (uint i = 0; i < len; ++i)
            {
                array[i] = obj.Get(i);
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

            for (uint i = 0; i < len; ++i)
            {
                obj.Put(i, array[i], false);
            }

            return obj.Target;
        }

        private JsValue Slice(JsValue thisObj, JsValue[] arguments)
        {
            var start = arguments.At(0);
            var end = arguments.At(1);

            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();

            var relativeStart = TypeConverter.ToInteger(start);
            uint k;
            if (relativeStart < 0)
            {
                k = (uint) System.Math.Max(len + relativeStart, 0);
            }
            else
            {
                k = (uint) System.Math.Min(TypeConverter.ToInteger(start), len);
            }

            uint final;
            if (end.IsUndefined())
            {
                final = TypeConverter.ToUint32(len);
            }
            else
            {
                double relativeEnd = TypeConverter.ToInteger(end);
                if (relativeEnd < 0)
                {
                    final = (uint) System.Math.Max(len + relativeEnd, 0);
                }
                else
                {
                    final = (uint) System.Math.Min(TypeConverter.ToInteger(relativeEnd), len);
                }
            }

            var a = Engine.Array.Construct(final - k);
            uint n = 0;
            for (; k < final; k++)
            {
                if (o.TryGetValue(k, out var kValue))
                {
                    a.SetIndexValue(n, kValue, updateLength: true);
                }

                n++;
            }

            return a;
        }

        private JsValue Shift(JsValue thisObj, JsValue[] arg2)
        {
            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();
            if (len == 0)
            {
                o.SetLength(0);
                return Undefined;
            }

            var first = o.Get(0);
            for (uint k = 1; k < len; k++)
            {
                var to = k - 1;
                if (o.TryGetValue(k, out var fromVal))
                {
                    o.Put(to, fromVal, true);
                }
                else
                {
                    o.DeleteAt(to);
                }
            }

            o.DeleteAt(len - 1);
            o.SetLength(len - 1);

            return first;
        }

        private JsValue Reverse(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();
            var middle = (uint) System.Math.Floor(len / 2.0);
            uint lower = 0;
            while (lower != middle)
            {
                var upper = len - lower - 1;
                var lowerExists = o.TryGetValue(lower, out var lowerValue);
                var upperExists = o.TryGetValue(upper, out var upperValue);
                if (lowerExists && upperExists)
                {
                    o.Put(lower, upperValue, true);
                    o.Put(upper, lowerValue, true);
                }

                if (!lowerExists && upperExists)
                {
                    o.Put(lower, upperValue, true);
                    o.DeleteAt(upper);
                }

                if (lowerExists && !upperExists)
                {
                    o.DeleteAt(lower);
                    o.Put(upper, lowerValue, true);
                }

                lower++;
            }

            return o.Target;
        }

        private JsValue Join(JsValue thisObj, JsValue[] arguments)
        {
            var separator = arguments.At(0);
            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();
            if (separator.IsUndefined())
            {
                separator = ",";
            }

            var sep = TypeConverter.ToString(separator);

            // as per the spec, this has to be called after ToString(separator)
            if (len == 0)
            {
                return "";
            }

            string StringFromJsValue(JsValue value)
            {
                return value.IsUndefined() || value.IsNull()
                    ? ""
                    : TypeConverter.ToString(value);
            }

            var s = StringFromJsValue(o.Get(0));
            if (len == 1)
            {
                return s;
            }

            var sb = ArrayExecutionContext.Current.StringBuilder;
            sb.Clear();
            sb.Append(s);
            for (uint k = 1; k < len; k++)
            {
                sb.Append(sep);
                sb.Append(StringFromJsValue(o.Get(k)));
            }

            return sb.ToString();
        }

        private JsValue ToLocaleString(JsValue thisObj, JsValue[] arguments)
        {
            var array = ArrayOperations.For(Engine, thisObj);
            var len = array.GetLength();
            const string separator = ",";
            if (len == 0)
            {
                return "";
            }

            JsValue r;
            if (!array.TryGetValue(0, out var firstElement) || firstElement.IsNull() || firstElement.IsUndefined())
            {
                r = "";
            }
            else
            {
                var elementObj = TypeConverter.ToObject(Engine, firstElement);
                var func = elementObj.Get("toLocaleString").TryCast<ICallable>(x => throw new JavaScriptException(Engine.TypeError));

                r = func.Call(elementObj, Arguments.Empty);
            }

            for (uint k = 1; k < len; k++)
            {
                string s = r + separator;
                if (!array.TryGetValue(k, out var nextElement) || nextElement.IsNull())
                {
                    r = "";
                }
                else
                {
                    var elementObj = TypeConverter.ToObject(Engine, nextElement);
                    var func = elementObj.Get("toLocaleString").TryCast<ICallable>(x => throw new JavaScriptException(Engine.TypeError));
                    r = func.Call(elementObj, Arguments.Empty);
                }

                r = s + r;
            }

            return r;
        }

        private JsValue Concat(JsValue thisObj, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(Engine, thisObj);
            uint n = 0;
            var items = new List<JsValue>(arguments.Length + 1) {o};
            items.AddRange(arguments);

            // try to find best capacity
            uint capacity = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var eArray = items[i] as ArrayInstance;
                capacity += eArray?.GetLength() ?? (uint) 1;
            }

            var a = Engine.Array.ConstructFast(capacity);
            for (var i = 0; i < items.Count; i++)
            {
                var e = items[i];
                if (e is ArrayInstance eArray)
                {
                    var len = eArray.GetLength();
                    for (uint k = 0; k < len; k++)
                    {
                        if (eArray.TryGetValue(k, out var subElement))
                        {
                            a.SetIndexValue(n, subElement, updateLength: false);
                        }

                        n++;
                    }
                }
                else
                {
                    a.SetIndexValue(n, e, updateLength: false);
                    n++;
                }
            }

            // this is not in the specs, but is necessary in case the last element of the last
            // array doesn't exist, and thus the length would not be incremented
            a.DefineOwnProperty("length", new PropertyDescriptor(n, PropertyFlag.None), false);

            return a;
        }

        private JsValue ToString(JsValue thisObj, JsValue[] arguments)
        {
            var array = TypeConverter.ToObject(Engine, thisObj);
            ICallable func;
            func = array.Get("join").TryCast<ICallable>(x => { func = Engine.Object.PrototypeObject.Get("toString").TryCast<ICallable>(y => throw new ArgumentException()); });

            return func.Call(array, Arguments.Empty);
        }

        private JsValue ReduceRight(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var initialValue = arguments.At(1);

            var o = TypeConverter.ToObject(Engine, thisObj);
            var lenValue = o.Get("length");
            var len = TypeConverter.ToUint32(lenValue);

            var callable = GetCallable(callbackfn);

            if (len == 0 && arguments.Length < 2)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            int k = (int) len - 1;
            JsValue accumulator = Undefined;
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
                var pk = TypeConverter.ToString(k);
                var kPresent = o.HasProperty(pk);
                if (kPresent)
                {
                    var kvalue = o.Get(pk);
                    accumulator = callable.Call(Undefined, new[] {accumulator, kvalue, k, o});
                }
            }

            return accumulator;
        }

        public JsValue Push(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject is ArrayInstance arrayInstance)
            {
                return arrayInstance.Push(arguments);
            }

            var o = TypeConverter.ToObject(Engine, thisObject);
            var lenVal = TypeConverter.ToNumber(o.Get("length"));

            // cast to double as we need to prevent an overflow
            double n = TypeConverter.ToUint32(lenVal);
            for (var i = 0; i < arguments.Length; i++)
            {
                o.Put(TypeConverter.ToString(n), arguments[i], true);
                n++;
            }

            o.Put("length", n, true);
            return n;
        }

        public JsValue Pop(JsValue thisObject, JsValue[] arguments)
        {
            var o = ArrayOperations.For(Engine, thisObject);
            var lenVal = TypeConverter.ToNumber(o.Target.Get("length"));

            uint len = TypeConverter.ToUint32(lenVal);
            if (len == 0)
            {
                o.SetLength(0);
                return Undefined;
            }

            len = len - 1;
            string indx = TypeConverter.ToString(len);
            JsValue element = o.Target.Get(indx);
            o.Target.Delete(indx, true);
            o.Target.Put("length", len, true);
            return element;
        }
        
        /// <summary>
        /// Adapter to use optimized array operations when possible.
        /// Gaps the difference between ArgumensInstance and ArrayInstance.
        /// </summary>
        private abstract class ArrayOperations
        {
            public abstract ObjectInstance Target { get; }

            public virtual uint GetSmallestIndex() => 0;

            public abstract uint GetLength();

            public abstract void SetLength(uint length);

            public virtual void EnsureCapacity(uint capacity)
            {
            }

            public abstract JsValue Get(uint index);

            public abstract bool TryGetValue(uint index, out JsValue value);

            public abstract void Put(uint index, JsValue value, bool throwOnError);

            public abstract void DeleteAt(uint index);

            public static ArrayOperations For(Engine engine, JsValue thisObj)
            {
                var instance = TypeConverter.ToObject(engine, thisObj);
                return For(instance);
            }

            public static ArrayOperations For(ObjectInstance instance)
            {
                if (instance is ArrayInstance arrayInstance)
                {
                    return new ArrayInstanceOperations(arrayInstance);
                }

                return new ObjectInstanceOperations(instance);
            }

            private class ObjectInstanceOperations : ArrayOperations
            {
                private readonly ObjectInstance _instance;

                public ObjectInstanceOperations(ObjectInstance instance)
                {
                    _instance = instance;
                }

                public override ObjectInstance Target => _instance;

                public override uint GetLength()
                {
                    var desc = _instance.GetProperty("length");
                    var descValue = desc.Value;
                    if (desc.IsDataDescriptor() && !ReferenceEquals(descValue, null))
                    {
                        return TypeConverter.ToUint32(descValue);
                    }

                    var getter = desc.Get ?? Undefined;
                    if (getter.IsUndefined())
                    {
                        return 0;
                    }

                    // if getter is not undefined it must be ICallable
                    var callable = (ICallable) getter;
                    var value = callable.Call(_instance, Arguments.Empty);
                    return TypeConverter.ToUint32(value);
                }

                public override void SetLength(uint length) => _instance.Put("length", length, true);

                public override JsValue Get(uint index) => _instance.Get(TypeConverter.ToString(index));

                public override bool TryGetValue(uint index, out JsValue value)
                {
                    var property = TypeConverter.ToString(index);
                    var kPresent = _instance.HasProperty(property);
                    value = kPresent ? _instance.Get(property) : Undefined;
                    return kPresent;
                }

                public override void Put(uint index, JsValue value, bool throwOnError) => _instance.Put(TypeConverter.ToString(index), value, throwOnError);

                public override void DeleteAt(uint index) => _instance.Delete(TypeConverter.ToString(index), true);
            }

            private class ArrayInstanceOperations : ArrayOperations
            {
                private readonly ArrayInstance _array;

                public ArrayInstanceOperations(ArrayInstance array)
                {
                    _array = array;
                }

                public override ObjectInstance Target => _array;

                public override uint GetSmallestIndex() => _array.GetSmallestIndex();

                public override uint GetLength() => _array.GetLength();

                public override void SetLength(uint length) => _array.Put("length", length, true);

                public override void EnsureCapacity(uint capacity)
                {
                    _array.EnsureCapacity(capacity);
                }

                public override bool TryGetValue(uint index, out JsValue value)
                {
                    return _array.TryGetValue(index, out value);
                }

                public override JsValue Get(uint index) => _array.Get(TypeConverter.ToString(index));

                public override void DeleteAt(uint index) => _array.DeleteAt(index);

                public override void Put(uint index, JsValue value, bool throwOnError) => _array.SetIndexValue(index, value, throwOnError);
            }
        }
    }
}