using System;
using System.Collections.Generic;
using Jint.Native.Object;
using Jint.Native.Symbol;
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

            obj.SetOwnProperty("length", new PropertyDescriptor(0, PropertyFlag.Writable));
            obj.SetOwnProperty("constructor", new PropertyDescriptor(arrayConstructor, PropertyFlag.NonEnumerable));

            return obj;
        }

        public void Configure()
        {
            FastAddProperty("toString", new ClrFunctionInstance(Engine, "toString", ToString, 0), true, false, true);
            FastAddProperty("toLocaleString", new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString), true, false, true);
            FastAddProperty("concat", new ClrFunctionInstance(Engine, "concat", Concat, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("copyWithin", new ClrFunctionInstance(Engine, "copyWithin", CopyWithin, 2, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("entries", new ClrFunctionInstance(Engine, "entries", Iterator, 0, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("fill", new ClrFunctionInstance(Engine, "fill", Fill, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("join", new ClrFunctionInstance(Engine, "join", Join, 1), true, false, true);
            FastAddProperty("pop", new ClrFunctionInstance(Engine, "pop", Pop), true, false, true);
            FastAddProperty("push", new ClrFunctionInstance(Engine, "push", Push, 1), true, false, true);
            FastAddProperty("reverse", new ClrFunctionInstance(Engine, "reverse", Reverse), true, false, true);
            FastAddProperty("shift", new ClrFunctionInstance(Engine, "shift", Shift), true, false, true);
            FastAddProperty("slice", new ClrFunctionInstance(Engine, "slice", Slice, 2), true, false, true);
            FastAddProperty("sort", new ClrFunctionInstance(Engine, "sort", Sort, 1), true, false, true);
            FastAddProperty("splice", new ClrFunctionInstance(Engine, "splice", Splice, 2), true, false, true);
            FastAddProperty("unshift", new ClrFunctionInstance(Engine, "unshift", Unshift, 1), true, false, true);
            FastAddProperty("includes", new ClrFunctionInstance(Engine, "includes", Includes, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("indexOf", new ClrFunctionInstance(Engine, "indexOf", IndexOf, 1), true, false, true);
            FastAddProperty("lastIndexOf", new ClrFunctionInstance(Engine, "lastIndexOf", LastIndexOf, 1), true, false, true);
            FastAddProperty("every", new ClrFunctionInstance(Engine, "every", Every, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("some", new ClrFunctionInstance(Engine, "some", Some, 1), true, false, true);
            FastAddProperty("forEach", new ClrFunctionInstance(Engine, "forEach", ForEach, 1), true, false, true);
            FastAddProperty("map", new ClrFunctionInstance(Engine, "map", Map, 1), true, false, true);
            FastAddProperty("filter", new ClrFunctionInstance(Engine, "filter", Filter, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("reduce", new ClrFunctionInstance(Engine, "reduce", Reduce, 1), true, false, true);
            FastAddProperty("reduceRight", new ClrFunctionInstance(Engine, "reduceRight", ReduceRight, 1), true, false, true);
            FastAddProperty("find", new ClrFunctionInstance(Engine, "find", Find, 1, PropertyFlag.Configurable), true, false, true);
            FastAddProperty("findIndex", new ClrFunctionInstance(Engine, "findIndex", FindIndex, 1, PropertyFlag.Configurable), true, false, true);

            FastAddProperty("values", new ClrFunctionInstance(Engine, "values", Iterator, 1), true, false, true);
            FastAddProperty(GlobalSymbolRegistry.Iterator._value, new ClrFunctionInstance(Engine, "iterator", Iterator, 1), true, false, true);
        }

        private ObjectInstance Iterator(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj is ObjectInstance oi)
            {
                return _engine.Iterator.Construct(oi);
            }

            return ExceptionHelper.ThrowTypeError<ObjectInstance>(_engine, "cannot construct iterator");
        }

        private JsValue Fill(JsValue thisObj, JsValue[] arguments)
        {
            var target = thisObj as ObjectInstance;
            var operations = ArrayOperations.For(target);
            var length = operations.GetLength();

            var value = arguments.At(0);

            var start = ConvertAndCheckForInfinity(arguments.At(1), 0);

            var relativeStart = TypeConverter.ToInteger(start);
            uint actualStart;
            if (relativeStart < 0)
            {
                actualStart = (uint) System.Math.Max(length + relativeStart, 0);
            }
            else
            {
                actualStart = (uint) System.Math.Min(relativeStart, length);
            }

            var end = ConvertAndCheckForInfinity(arguments.At(2), length);
            var relativeEnd = TypeConverter.ToInteger(end);
            uint actualEnd;
            if (relativeEnd < 0)
            {
                actualEnd = (uint) System.Math.Max(length + relativeEnd, 0);
            }
            else
            {
                actualEnd = (uint) System.Math.Min(relativeEnd, length);
            }

            for (var i = actualStart; i < actualEnd; ++i)
            {
                operations.Put(i, value, false);
            }

            return target;
        }

        private JsValue CopyWithin(JsValue thisObj, JsValue[] arguments)
        {
            // Steps 1-2.
            if (thisObj.IsNullOrUndefined() || !(thisObj is ArrayInstance array))
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "this is null or not defined");
            }

            JsValue target = arguments.At(0);
            JsValue start = arguments.At(1);
            JsValue end = arguments.At(2);
            
            var initialLength = ArrayOperations.For(array).GetLength();
            var len = initialLength >> 0;

            var relativeTarget = ConvertAndCheckForInfinity(target, 0);

            var to = relativeTarget < 0 ?
                (uint) System.Math.Max(len + relativeTarget, 0) :
                (uint) System.Math.Min(relativeTarget, len);

            var relativeStart = ConvertAndCheckForInfinity(start, 0);

            var from = relativeStart < 0 ?
                (uint) System.Math.Max(len + relativeStart, 0) :
                (uint) System.Math.Min(relativeStart, len);

            var relativeEnd = ConvertAndCheckForInfinity(end, len);

            var final = relativeEnd < 0 ?
                System.Math.Max(len + relativeEnd, 0) :
                System.Math.Min(relativeEnd, len);

            var count = System.Math.Min(final - from, len - to);

            var direction = 1;

            if (from < to && to < from + count) {
                direction = -1;
                from += (uint) count - 1;
                to += (uint) count - 1;
            }

            while (count > 0)
            {
                if (array.TryGetValue(from, out var value))
                {
                    array.SetIndexValue(to, value, false);
                }
                else
                {
                    array.RemoveOwnProperty(TypeConverter.ToString(to));
                }
                from = (uint) (from + direction);
                to = (uint) (to + direction);
                count--;
            }

            return thisObj;
        }

        long ConvertAndCheckForInfinity(JsValue jsValue, uint defaultValue)
        {
            if (jsValue.IsUndefined())
            {
                return defaultValue;
            }

            var num = TypeConverter.ToNumber(jsValue);
            if (double.IsInfinity(num))
            {
                return defaultValue;
            }

            return (long) num;
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
                ExceptionHelper.ThrowTypeError(Engine);
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
                    ExceptionHelper.ThrowTypeError(Engine);
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
            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o.Target;
            for (uint k = 0; k < len; k++)
            {
                if (o.TryGetValue(k, out var kvalue))
                {
                    args[0] = kvalue;
                    args[1] = k;
                    var selected = callable.Call(thisArg, args);
                    if (TypeConverter.ToBoolean(selected))
                    {
                        a.SetIndexValue(to, kvalue, updateLength: false);
                        to++;
                    }
                }
            }

            a.SetLength(to);
            _engine._jsValueArrayPool.ReturnArray(args);

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

            var a = Engine.Array.ConstructFast(len);
            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o.Target;
            for (uint k = 0; k < len; k++)
            {
                if (o.TryGetValue(k, out var kvalue))
                {
                    args[0] = kvalue;
                    args[1] = k;
                    var mappedValue = callable.Call(thisArg, args);
                    a.SetIndexValue(k, mappedValue, updateLength: false);
                }
            }
            _engine._jsValueArrayPool.ReturnArray(args);
            return a;
        }

        private JsValue ForEach(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLength();

            var callable = GetCallable(callbackfn);

            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o.Target;
            for (uint k = 0; k < len; k++)
            {
                if (o.TryGetValue(k, out var kvalue))
                {
                    args[0] = kvalue;
                    args[1] = k;
                    callable.Call(thisArg, args);
                }
            }
            _engine._jsValueArrayPool.ReturnArray(args);

            return Undefined;
        }

        private JsValue Includes(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLongLength();

            if (len == 0)
            {
                return false;
            }

            var searchElement = arguments.At(0);
            var fromIndex = arguments.At(1, 0);
            
            var n = TypeConverter.ToNumber(fromIndex);
            n = n > ArrayOperations.MaxObjectLength
                ? ArrayOperations.MaxObjectLength
                : n;

            var k = (ulong) System.Math.Max(
                n >= 0 
                    ? n
                    : len - System.Math.Abs(n), 0);

            bool SameValueZero(JsValue x, JsValue y) {
                return x == y 
                             || (x is JsNumber xNum && y is JsNumber yNum && double.IsNaN(xNum._value) && double.IsNaN(yNum._value));
            }

            while (k < len)
            {
                o.TryGetValue(k, out var value);
                if (SameValueZero(value, searchElement))
                {
                    return true;
                }
                k++;
            }
            return false;
        }

        private JsValue Some(JsValue thisObj, JsValue[] arguments)
        {
            var target = TypeConverter.ToObject(Engine, thisObj);
            return target.FindWithCallback(arguments, out _, out _);
        }

        private JsValue Every(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(Engine, thisObj);
            uint len;
            if (thisObj is ArrayInstance arrayInstance)
            {
                len = arrayInstance.GetLength();
            }
            else
            {
                var intValue = ((ArrayOperations.ObjectInstanceOperations) o).GetIntegerLength();
                len = intValue < 0 ? 0 : (uint) intValue;
            }

            if (len == 0)
            {
                return JsBoolean.True;
            }

            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);
            var callable = GetCallable(callbackfn);

            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o.Target;
            for (uint k = 0; k < len; k++)
            {
                if (o.TryGetValue(k, out var kvalue))
                {
                    args[0] = kvalue;
                    args[1] = k;
                    var testResult = callable.Call(thisArg, args);
                    if (false == TypeConverter.ToBoolean(testResult))
                    {
                        return JsBoolean.False;
                    }
                }
            }
            _engine._jsValueArrayPool.ReturnArray(args);

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

            var items = System.ArrayExt.Empty<JsValue>();
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
                ExceptionHelper.ThrowTypeError(_engine, "Array.prorotype.sort can only be applied on objects");
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
                compareFn = compareArg.TryCast<ICallable>(x => ExceptionHelper.ThrowTypeError(_engine, "The sort argument must be a function"));
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

        internal JsValue Slice(JsValue thisObj, JsValue[] arguments)
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
                return value.IsNullOrUndefined()
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
                var func = elementObj.Get("toLocaleString") as ICallable ?? ExceptionHelper.ThrowTypeError<ICallable>(_engine);

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
                    var func = elementObj.Get("toLocaleString") as ICallable ?? ExceptionHelper.ThrowTypeError<ICallable>(_engine);
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
            bool hasNonSpreadables = false;
            uint capacity = 0;
            for (var i = 0; i < items.Count; i++)
            {
                uint increment;
                var objectInstance = items[i] as ObjectInstance;
                if (objectInstance == null
                    || (hasNonSpreadables |= objectInstance.IsConcatSpreadable) == false)
                {
                    increment = 1;
                }
                else
                {
                    var operations = ArrayOperations.For(objectInstance);
                    increment = operations.GetLength();
                }
                capacity += increment;
            }

            var a = Engine.Array.ConstructFast(capacity);
            for (var i = 0; i < items.Count; i++)
            {
                var e = items[i];
                if (e is ArrayInstance eArray
                    && (!hasNonSpreadables || eArray.IsConcatSpreadable))
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
                else if (hasNonSpreadables
                         && e is ObjectInstance oi 
                         && oi.IsConcatSpreadable)
                {
                    var operations = ArrayOperations.For(oi);
                    var len = operations.GetLength();
                    for (uint k = 0; k < len; k++)
                    {
                        operations.TryGetValue(k, out var subElement);
                        a.SetIndexValue(n, subElement, updateLength: false);
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
            func = array.Get("join").TryCast<ICallable>(x =>
            {
                func = Engine.Object.PrototypeObject.Get("toString").TryCast<ICallable>(y => ExceptionHelper.ThrowArgumentException());
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

            var callable = GetCallable(callbackfn);

            if (len == 0 && arguments.Length < 2)
            {
                ExceptionHelper.ThrowTypeError(Engine);
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
                    ExceptionHelper.ThrowTypeError(Engine);
                }
            }

            var jsValues = new JsValue[4];
            for (; k >= 0; k--)
            {
                var pk = TypeConverter.ToString(k);
                var kPresent = o.HasProperty(pk);
                if (kPresent)
                {
                    var kvalue = o.Get(pk);
                    jsValues[0] = accumulator;
                    jsValues[1] = kvalue;
                    jsValues[2] = k;
                    jsValues[3] = o;
                    accumulator = callable.Call(Undefined, jsValues);
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
        /// Gaps the difference between ArgumentsInstance and ArrayInstance.
        /// </summary>
        internal abstract class ArrayOperations
        {
            protected internal const long MaxObjectLength = 9007199254740991;

            public abstract ObjectInstance Target { get; }

            public virtual uint GetSmallestIndex() => 0;

            public abstract uint GetLength();

            public abstract ulong GetLongLength();

            public abstract void SetLength(uint length);

            public virtual void EnsureCapacity(uint capacity)
            {
            }

            public abstract JsValue Get(uint index);

            public abstract bool TryGetValue(uint index, out JsValue value);

            public abstract bool TryGetValue(ulong index, out JsValue value);

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

            internal sealed class ObjectInstanceOperations : ArrayOperations
            {
                private readonly ObjectInstance _instance;

                public ObjectInstanceOperations(ObjectInstance instance)
                {
                    _instance = instance;
                }

                public override ObjectInstance Target => _instance;

                internal double GetIntegerLength()
                {
                    var desc = _instance.GetProperty("length");
                    var descValue = desc.Value;
                    if (desc.IsDataDescriptor() && !ReferenceEquals(descValue, null))
                    {
                        return TypeConverter.ToInteger(descValue);
                    }

                    var getter = desc.Get ?? Undefined;
                    if (getter.IsUndefined())
                    {
                        return 0;
                    }

                    // if getter is not undefined it must be ICallable
                    var callable = (ICallable) getter;
                    var value = callable.Call(_instance, Arguments.Empty);
                    return (uint) TypeConverter.ToInteger(value);
                }

                public override uint GetLength()
                {
                    var integerLength = GetIntegerLength();
                    return (uint) (integerLength >= 0 ? integerLength : 0);
                }

                public override ulong GetLongLength()
                {
                    var integerLength = GetIntegerLength();
                    if (integerLength <= 0)
                    {
                        return 0;
                    }
                    return (ulong) System.Math.Min(integerLength, MaxObjectLength);
                }

                public override void SetLength(uint length) => _instance.Put("length", length, true);

                public override JsValue Get(uint index) => _instance.Get(TypeConverter.ToString(index));

                public override bool TryGetValue(uint index, out JsValue value)
                {
                    return TryGetValue((ulong) index, out value);
                }

                public override bool TryGetValue(ulong index, out JsValue value)
                {
                    var property = TypeConverter.ToString(index);
                    var kPresent = _instance.HasProperty(property);
                    value = kPresent ? _instance.Get(property) : Undefined;
                    return kPresent;
                }

                public override void Put(uint index, JsValue value, bool throwOnError) => _instance.Put(TypeConverter.ToString(index), value, throwOnError);

                public override void DeleteAt(uint index) => _instance.Delete(TypeConverter.ToString(index), true);
            }

            private sealed class ArrayInstanceOperations : ArrayOperations
            {
                private readonly ArrayInstance _array;

                public ArrayInstanceOperations(ArrayInstance array)
                {
                    _array = array;
                }

                public override ObjectInstance Target => _array;

                public override uint GetSmallestIndex() => _array.GetSmallestIndex();

                public override uint GetLength()
                {
                    return (uint) ((JsNumber) _array._length._value)._value;
                }

                public override ulong GetLongLength()
                {
                    return (ulong) ((JsNumber) _array._length._value)._value;
                }

                public override void SetLength(uint length) => _array.Put("length", length, true);

                public override void EnsureCapacity(uint capacity)
                {
                    _array.EnsureCapacity(capacity);
                }

                public override bool TryGetValue(uint index, out JsValue value)
                {
                    return _array.TryGetValue(index, out value);
                }

                public override bool TryGetValue(ulong index, out JsValue value)
                {
                    // array max size is uint
                    return _array.TryGetValue((uint) index, out value);
                }

                public override JsValue Get(uint index) => _array.Get(TypeConverter.ToString(index));

                public override void DeleteAt(uint index) => _array.DeleteAt(index);

                public override void Put(uint index, JsValue value, bool throwOnError) => _array.SetIndexValue(index, value, throwOnError);
            }
        }
    }
}