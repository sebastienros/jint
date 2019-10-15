using System;
using System.Collections.Generic;
using Jint.Collections;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter.Expressions;

using static System.String;

namespace Jint.Native.Array
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.4.4
    /// </summary>
    public sealed class ArrayPrototype : ArrayInstance
    {
        private ArrayConstructor _arrayConstructor;

        private ArrayPrototype(Engine engine) : base(engine)
        {
        }

        public static ArrayPrototype CreatePrototypeObject(Engine engine, ArrayConstructor arrayConstructor)
        {
            var obj = new ArrayPrototype(engine)
            {
                Extensible = true,
                Prototype = engine.Object.PrototypeObject,
                _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Writable),
                _arrayConstructor = arrayConstructor,
            };


            return obj;
        }

        protected override void Initialize()
        {
            _properties = new StringDictionarySlim<PropertyDescriptor>(35)
            {
                ["constructor"] = new PropertyDescriptor(_arrayConstructor, PropertyFlag.NonEnumerable),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToString, 0, PropertyFlag.Configurable), true, false, true),
                ["toLocaleString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString, 0, PropertyFlag.Configurable), true, false, true),
                ["concat"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "concat", Concat, 1, PropertyFlag.Configurable), true, false, true),
                ["copyWithin"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "copyWithin", CopyWithin, 2, PropertyFlag.Configurable), true, false, true),
                ["entries"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "entries", Iterator, 0, PropertyFlag.Configurable), true, false, true),
                ["fill"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "fill", Fill, 1, PropertyFlag.Configurable), true, false, true),
                ["join"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "join", Join, 1, PropertyFlag.Configurable), true, false, true),
                ["pop"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "pop", Pop, 0, PropertyFlag.Configurable), true, false, true),
                ["push"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "push", Push, 1, PropertyFlag.Configurable), true, false, true),
                ["reverse"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "reverse", Reverse, 0, PropertyFlag.Configurable), true, false, true),
                ["shift"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "shift", Shift, 0, PropertyFlag.Configurable), true, false, true),
                ["slice"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "slice", Slice, 2, PropertyFlag.Configurable), true, false, true),
                ["sort"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "sort", Sort, 1, PropertyFlag.Configurable), true, false, true),
                ["splice"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "splice", Splice, 2, PropertyFlag.Configurable), true, false, true),
                ["unshift"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "unshift", Unshift, 1, PropertyFlag.Configurable), true, false, true),
                ["includes"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "includes", Includes, 1, PropertyFlag.Configurable), true, false, true),
                ["indexOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "indexOf", IndexOf, 1, PropertyFlag.Configurable), true, false, true),
                ["lastIndexOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "lastIndexOf", LastIndexOf, 1, PropertyFlag.Configurable), true, false, true),
                ["every"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "every", Every, 1, PropertyFlag.Configurable), true, false, true),
                ["some"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "some", Some, 1, PropertyFlag.Configurable), true, false, true),
                ["forEach"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "forEach", ForEach, 1, PropertyFlag.Configurable), true, false, true),
                ["map"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "map", Map, 1, PropertyFlag.Configurable), true, false, true),
                ["filter"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "filter", Filter, 1, PropertyFlag.Configurable), true, false, true),
                ["reduce"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "reduce", Reduce, 1, PropertyFlag.Configurable), true, false, true),
                ["reduceRight"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "reduceRight", ReduceRight, 1, PropertyFlag.Configurable), true, false, true),
                ["find"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "find", Find, 1, PropertyFlag.Configurable), true, false, true),
                ["findIndex"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "findIndex", FindIndex, 1, PropertyFlag.Configurable), true, false, true),
                ["keys"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "keys", Keys, 0, PropertyFlag.Configurable), true, false, true),
                ["values"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "values", Values, 0, PropertyFlag.Configurable), true, false, true),
                [GlobalSymbolRegistry.Iterator._value] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "iterator", Values, 1), true, false, true)
            };
        }
        
        private ObjectInstance Keys(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj is ObjectInstance oi && oi.IsArrayLike)
            {
                return _engine.Iterator.ConstructArrayLikeKeyIterator(oi);
            }

            return ExceptionHelper.ThrowTypeError<ObjectInstance>(_engine, "cannot construct iterator");
        }
        
        private ObjectInstance Values(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj is ObjectInstance oi && oi.IsArrayLike)
            {
                return _engine.Iterator.ConstructArrayLikeValueIterator(oi);
            }

            return ExceptionHelper.ThrowTypeError<ObjectInstance>(_engine, "cannot construct iterator");
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
            if (thisObj.IsNullOrUndefined())
            {
                ExceptionHelper.ThrowTypeError(_engine, "Cannot convert undefined or null to object");
            }

            var operations = ArrayOperations.For(thisObj as ObjectInstance);
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

            return thisObj;
        }

        private JsValue CopyWithin(JsValue thisObj, JsValue[] arguments)
        {
            // Steps 1-2.
            if (thisObj.IsNullOrUndefined())
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "this is null or not defined");
            }

            JsValue target = arguments.At(0);
            JsValue start = arguments.At(1);
            JsValue end = arguments.At(2);

            var operations = ArrayOperations.For(thisObj as ObjectInstance);
            var initialLength = operations.GetLength();
            var len = ConvertAndCheckForInfinity(initialLength, 0);

            var relativeTarget = ConvertAndCheckForInfinity(target, 0);

            var to = relativeTarget < 0 ?
                System.Math.Max(len + relativeTarget, 0) :
                System.Math.Min(relativeTarget, len);

            var relativeStart = ConvertAndCheckForInfinity(start, 0);

            var from = relativeStart < 0 ?
                System.Math.Max(len + relativeStart, 0) :
                System.Math.Min(relativeStart, len);

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
                if (operations.TryGetValue((ulong) from, out var value))
                {
                    operations.Put((ulong) to, value, false);
                }
                else
                {
                    operations.DeleteAt((ulong) to);
                }
                from = (uint) (from + direction);
                to = (uint) (to + direction);
                count--;
            }

            return thisObj;
        }

        long ConvertAndCheckForInfinity(JsValue jsValue, long defaultValue)
        {
            if (jsValue.IsUndefined())
            {
                return defaultValue;
            }

            var num = TypeConverter.ToNumber(jsValue);

            if (double.IsPositiveInfinity(num))
            {
                return long.MaxValue;
            }

            return (long) num;
        }

        private JsValue LastIndexOf(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLongLength();
            if (len == 0)
            {
                return -1;
            }

            var n = arguments.Length > 1 
                ? TypeConverter.ToInteger(arguments[1]) 
                : len - 1;
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
                    var same = JintBinaryExpression.StrictlyEqual(value, searchElement);
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
            var len = o.GetLongLength();

            if (len > ArrayOperations.MaxArrayLength)
            {
                ExceptionHelper.ThrowRangeError(_engine, "Invalid array length");;
            }

            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);
            var callable = GetCallable(callbackfn);

            var a = Engine.Array.ConstructFast((uint) len);
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
            n = n > ArrayOperations.MaxArrayLikeLength
                ? ArrayOperations.MaxArrayLikeLength
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
            return target.FindWithCallback(arguments, out _, out _, false);
        }

        private JsValue Every(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(Engine, thisObj);
            ulong len = o.GetLongLength();

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
            var len = o.GetLongLength();
            if (len == 0)
            {
                return -1;
            }

            var startIndex = arguments.Length > 1
                ? TypeConverter.ToNumber(arguments[1]) 
                : 0;

            if (startIndex > uint.MaxValue)
            {
                return -1;
            }

            ulong k;
            if (startIndex < 0)
            {
                var abs = System.Math.Abs(startIndex);
                ulong temp = len - (uint) abs;
                if (abs > len || temp < 0)
                {
                    temp = 0;
                }

                k = temp;
            }
            else
            {
                k = (ulong) startIndex;
            }

            if (k >= len)
            {
                return -1;
            }

            ulong smallestIndex = o.GetSmallestIndex(len);
            if (smallestIndex > k)
            {
                k = smallestIndex;
            }

            var searchElement = arguments.At(0);
            for (; k < len; k++)
            {
                if (o.TryGetValue(k, out var elementK))
                {
                    var same = JintBinaryExpression.StrictlyEqual(elementK, searchElement);
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
            target.FindWithCallback(arguments, out _, out var value, true);
            return value;
        }

        private JsValue FindIndex(JsValue thisObj, JsValue[] arguments)
        {
            var target = TypeConverter.ToObject(Engine, thisObj);
            if (target.FindWithCallback(arguments, out var index, out _, true))
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
            var len = o.GetLongLength();
            var relativeStart = TypeConverter.ToInteger(start);

            ulong actualStart;
            if (relativeStart < 0)
            {
                actualStart = (ulong) System.Math.Max(len + relativeStart, 0);
            }
            else
            {
                actualStart = (ulong) System.Math.Min(relativeStart, len);
            }

            var items = ArrayExt.Empty<JsValue>();
            ulong insertCount;
            ulong actualDeleteCount;
            if (arguments.Length == 0)
            {
                insertCount = 0;
                actualDeleteCount = 0;
            }
            else if (arguments.Length == 1)
            {
                insertCount = 0;
                actualDeleteCount = len - actualStart;
            }
            else
            {
                insertCount = (ulong) (arguments.Length - 2);
                var dc = TypeConverter.ToInteger(deleteCount);
                actualDeleteCount = (ulong) System.Math.Min(System.Math.Max(dc,0), len - actualStart);

                items = new JsValue[arguments.Length - 2];
                System.Array.Copy(arguments, 2, items, 0, items.Length);
            }

            if (len + insertCount - actualDeleteCount > ArrayOperations.MaxArrayLikeLength)
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "Invalid array length");
            }

            if (actualDeleteCount > ArrayOperations.MaxArrayLength)
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "Invalid array length");
            }
            
            var a = Engine.Array.ConstructFast((uint) actualDeleteCount);
            for (uint k = 0; k < actualDeleteCount; k++)
            {
                if (o.TryGetValue(actualStart + k, out var fromValue))
                {
                    a.SetIndexValue(k, fromValue, updateLength: false);
                }
            }
            a.SetLength((uint) actualDeleteCount);

            var length = len - actualDeleteCount + (uint) items.Length;
            o.EnsureCapacity(length);
            if ((ulong) items.Length < actualDeleteCount)
            {
                for (ulong k = actualStart; k < len - actualDeleteCount; k++)
                {
                    var from = k + actualDeleteCount;
                    var to = k + (ulong) items.Length;
                    if (o.TryGetValue(from, out var fromValue))
                    {
                        o.Put(to, fromValue, true);
                    }
                    else
                    {
                        o.DeleteAt(to);
                    }
                }

                for (var k = len; k > len - actualDeleteCount + (ulong) items.Length; k--)
                {
                    o.DeleteAt(k - 1);
                }
            }
            else if ((ulong) items.Length > actualDeleteCount)
            {
                for (var k = len - actualDeleteCount; k > actualStart; k--)
                {
                    var from = k + actualDeleteCount - 1;
                    var to =  k + (ulong) items.Length - 1;
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
            var len = o.GetLongLength();
            var argCount = (uint) arguments.Length;

            if (len + argCount > ArrayOperations.MaxArrayLikeLength)
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "Invalid array length");
            }

            o.EnsureCapacity(len + argCount);
            var minIndex = o.GetSmallestIndex(len);
            for (var k = len; k > minIndex; k--)
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

            var compareArg = arguments.At(0);
            ICallable compareFn = null;
            if (!compareArg.IsUndefined())
            {
                if (compareArg.IsNull() || !(compareArg is ICallable))
                {
                    ExceptionHelper.ThrowTypeError(_engine, "The comparison function must be either a function or undefined");
                }

                compareFn = (ICallable) compareArg;
            }

            var len = obj.GetLength();
            if (len <= 1)
            {
                return obj.Target;
            }

            int Comparer(JsValue x, JsValue y)
            {
                if (ReferenceEquals(x, null))
                {
                    return 1;
                }

                if (ReferenceEquals(y, null))
                {
                    return -1;
                }

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

                var r = CompareOrdinal(xString, yString);
                return r;
            }

            var array = new JsValue[len];
            for (uint i = 0; i < (uint) array.Length; ++i)
            {
                var value = obj.TryGetValue(i, out var temp)
                    ? temp
                    : null;
                array[i] = value;
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

            for (uint i = 0; i < (uint) array.Length; ++i)
            {
                if (!ReferenceEquals(array[i], null))
                {
                    obj.Put(i, array[i], false);
                }
                else
                {
                    obj.DeleteAt(i);
                }
            }

            return obj.Target;
        }

        internal JsValue Slice(JsValue thisObj, JsValue[] arguments)
        {
            var start = arguments.At(0);
            var end = arguments.At(1);

            var o = ArrayOperations.For(Engine, thisObj);
            var len = o.GetLongLength();

            var relativeStart = TypeConverter.ToInteger(start);
            ulong k;
            if (relativeStart < 0)
            {
                k = (ulong) System.Math.Max(len + relativeStart, 0);
            }
            else
            {
                k = (ulong) System.Math.Min(TypeConverter.ToInteger(start), len);
            }

            ulong final;
            if (end.IsUndefined())
            {
                final = (ulong) TypeConverter.ToNumber(len);
            }
            else
            {
                double relativeEnd = TypeConverter.ToInteger(end);
                if (relativeEnd < 0)
                {
                    final = (ulong) System.Math.Max(len + relativeEnd, 0);
                }
                else
                {
                    final = (ulong) System.Math.Min(TypeConverter.ToInteger(relativeEnd), len);
                }
            }

            if (k < final && final - k > ArrayOperations.MaxArrayLength)
            {
                ExceptionHelper.ThrowRangeError(_engine, "Invalid array length");;
            }

            var length = (uint) System.Math.Max(0, (long) final - (long) k);
            var a = Engine.Array.Construct(length);
            if (thisObj is ArrayInstance ai)
            {
                a.CopyValues(ai, (uint) k, 0, length);
            }
            else
            {
                // slower path
                for (uint n = 0; k < final; k++, n++)
                {
                    if (o.TryGetValue(k, out var kValue))
                    {
                        a.SetIndexValue(n, kValue, updateLength: false);
                    }
                }
            }
            a.DefineOwnProperty("length", new PropertyDescriptor(length, PropertyFlag.None), false);

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
            var len = o.GetLongLength();
            var middle = (ulong) System.Math.Floor(len / 2.0);
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

            using (var sb = StringBuilderPool.Rent())
            {
                sb.Builder.Append(s);
                for (uint k = 1; k < len; k++)
                {
                    sb.Builder.Append(sep);
                    sb.Builder.Append(StringFromJsValue(o.Get(k)));
                }

                return sb.ToString();
            }
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
            var items = new List<JsValue>(arguments.Length + 1) {o};
            items.AddRange(arguments);

            // try to find best capacity
            bool hasObjectSpreadables = false;
            uint capacity = 0;
            for (var i = 0; i < items.Count; i++)
            {
                uint increment;
                var objectInstance = items[i] as ObjectInstance;
                if (objectInstance == null)
                {
                    increment = 1;
                }
                else
                {
                    var isConcatSpreadable = objectInstance.IsConcatSpreadable;
                    hasObjectSpreadables |= isConcatSpreadable;
                    var operations = ArrayOperations.For(objectInstance);
                    increment = isConcatSpreadable ? operations.GetLength() : 1; 
                }
                capacity += increment;
            }

            uint n = 0;
            var a = Engine.Array.ConstructFast(capacity);
            for (var i = 0; i < items.Count; i++)
            {
                var e = items[i];
                if (e is ArrayInstance eArray
                    && eArray.IsConcatSpreadable)
                {
                    a.CopyValues(eArray, 0, n, eArray.GetLength());
                    n += eArray.GetLength();
                }
                else if (hasObjectSpreadables
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

            if (array.IsArrayLike == false || func == null)
                return _engine.Object.PrototypeObject.ToObjectString(array, Arguments.Empty);

            return func.Call(array, Arguments.Empty);
        }

        private JsValue ReduceRight(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var initialValue = arguments.At(1);

            var o = ArrayOperations.For(TypeConverter.ToObject(_engine, thisObj));
            var len = o.GetLongLength();

            var callable = GetCallable(callbackfn);

            if (len == 0 && arguments.Length < 2)
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            long k = (long) (len - 1);
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
                    if ((kPresent = o.TryGetValue((ulong) k, out var temp)))
                    {
                        accumulator = temp;
                    }

                    k--;
                }

                if (kPresent == false)
                {
                    ExceptionHelper.ThrowTypeError(Engine);
                }
            }

            var jsValues = new JsValue[4];
            jsValues[3] = o.Target;
            for (; k >= 0; k--)
            {
                if (o.TryGetValue((ulong) k, out var kvalue))
                {
                    jsValues[0] = accumulator;
                    jsValues[1] = kvalue;
                    jsValues[2] = k;
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

            var o = ArrayOperations.For(thisObject as ObjectInstance);
            var n = o.GetLongLength();

            if (n + (ulong) arguments.Length > ArrayOperations.MaxArrayLikeLength)
            {
                return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "Invalid array length");
            }

            // cast to double as we need to prevent an overflow
            for (var i = 0; i < arguments.Length; i++)
            {
                o.Put(n, arguments[i], true);
                n++;
            }

            o.SetLength(n);
            return n;
        }

        public JsValue Pop(JsValue thisObject, JsValue[] arguments)
        {
            var o = ArrayOperations.For(Engine, thisObject);
            ulong len = o.GetLongLength();
            if (len == 0)
            {
                o.SetLength(0);
                return Undefined;
            }

            len = len - 1;
            JsValue element = o.Get(len);
            o.DeleteAt(len);
            o.SetLength(len);
            return element;
        }

        /// <summary>
        /// Adapter to use optimized array operations when possible.
        /// Gaps the difference between ArgumentsInstance and ArrayInstance.
        /// </summary>
        internal abstract class ArrayOperations
        {
            protected internal const ulong MaxArrayLength = 4294967295;
            protected internal const ulong MaxArrayLikeLength = NumberConstructor.MaxSafeInteger;

            public abstract ObjectInstance Target { get; }

            public abstract ulong GetSmallestIndex(ulong length);

            public abstract uint GetLength();

            public abstract ulong GetLongLength();

            public abstract void SetLength(ulong length);

            public abstract void EnsureCapacity(ulong capacity);

            public abstract JsValue Get(ulong index);

            public virtual JsValue[] GetAll()
            {
                var n = (int) GetLength();
                var jsValues = new JsValue[n];
                for (uint i = 0; i < (uint) jsValues.Length; i++)
                {
                    jsValues[i] = Get(i);
                }

                return jsValues;
            }

            public abstract bool TryGetValue(ulong index, out JsValue value);

            public abstract void Put(ulong index, JsValue value, bool throwOnError);

            public abstract void DeleteAt(ulong index);

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
                    return TypeConverter.ToInteger(value);
                }

                public override ulong GetSmallestIndex(ulong length)
                {
                    // there are some evil tests that iterate a lot with unshift..
                    if (_instance._properties == null)
                    {
                        return 0;
                    }

                    ulong min = length;
                    foreach (var entry in _instance._properties)
                    {
                        if (ulong.TryParse(entry.Key, out var index))
                        {
                            min = System.Math.Min(index, min);
                        }
                    }

                    if (_instance.Prototype?._properties != null)
                    {
                        foreach (var entry  in _instance.Prototype._properties)
                        {
                            if (ulong.TryParse(entry.Key, out var index))
                            {
                                min = System.Math.Min(index, min);
                            }
                        }
                    }

                    return min;
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
                    return (ulong) System.Math.Min(integerLength, MaxArrayLikeLength);
                }

                public override void SetLength(ulong length) => _instance.Put("length", length, true);

                public override void EnsureCapacity(ulong capacity)
                {
                }

                public override JsValue Get(ulong index) => _instance.Get(TypeConverter.ToString(index));

                public override bool TryGetValue(ulong index, out JsValue value)
                {
                    var property = TypeConverter.ToString(index);
                    var kPresent = _instance.HasProperty(property);
                    value = kPresent ? _instance.Get(property) : Undefined;
                    return kPresent;
                }

                public override void Put(ulong index, JsValue value, bool throwOnError) => _instance.Put(TypeConverter.ToString(index), value, throwOnError);

                public override void DeleteAt(ulong index) => _instance.Delete(TypeConverter.ToString(index), true);
            }

            private sealed class ArrayInstanceOperations : ArrayOperations
            {
                private readonly ArrayInstance _array;

                public ArrayInstanceOperations(ArrayInstance array)
                {
                    _array = array;
                }

                public override ObjectInstance Target => _array;

                public override ulong GetSmallestIndex(ulong length) => _array.GetSmallestIndex();

                public override uint GetLength()
                {
                    return (uint) ((JsNumber) _array._length._value)._value;
                }

                public override ulong GetLongLength()
                {
                    return (ulong) ((JsNumber) _array._length._value)._value;
                }

                public override void SetLength(ulong length) => _array.Put("length", length, true);

                public override void EnsureCapacity(ulong capacity)
                {
                    _array.EnsureCapacity((uint) capacity);
                }

                public override bool TryGetValue(ulong index, out JsValue value)
                {
                    // array max size is uint
                    return _array.TryGetValue((uint) index, out value);
                }

                public override JsValue Get(ulong index) => _array.Get((uint) index);

                public override JsValue[] GetAll()
                {
                    var n = _array.Length;

                    if (_array._dense == null || _array._dense.Length < n)
                    {
                        return base.GetAll();
                    }

                    // optimized
                    var jsValues = new JsValue[n];
                    for (uint i = 0; i < (uint) jsValues.Length; i++)
                    {
                        var prop = _array._dense[i] ?? PropertyDescriptor.Undefined;
                        if (prop == PropertyDescriptor.Undefined)
                        {
                            prop = _array.Prototype?.GetProperty(i) ?? PropertyDescriptor.Undefined;
                        }

                        jsValues[i] = _array.UnwrapJsValue(prop);
                    }

                    return jsValues;
                }

                public override void DeleteAt(ulong index) => _array.DeleteAt((uint) index);

                public override void Put(ulong index, JsValue value, bool throwOnError) => _array.SetIndexValue((uint) index, value, throwOnError);
            }
        }
    }
}