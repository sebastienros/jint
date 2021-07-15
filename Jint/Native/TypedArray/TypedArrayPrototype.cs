using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Collections;
using Jint.Native.ArrayBuffer;
using Jint.Native.Iterator;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Native.TypedArray
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-properties-of-the-%typedarrayprototype%-object
    /// </summary>
    public sealed class TypedArrayPrototype : ObjectInstance
    {
        private readonly Realm _realm;
        private readonly TypedArrayConstructor _constructor;
        private PropertyDescriptor _length;
        private ClrFunctionInstance _originalIteratorFunction;

        internal TypedArrayPrototype(
            Engine engine,
            Realm realm,
            ObjectInstance objectPrototype,
            TypedArrayConstructor constructor) : base(engine)
        {
            _prototype = objectPrototype;
            _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Writable);
            _realm = realm;
            _constructor = constructor;
        }

        protected override void Initialize()
        {
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            const PropertyFlag propertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
            var properties = new PropertyDictionary(30, checkExistingKeys: false)
            {
                ["buffer"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(_engine, "get buffer", Buffer, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
                ["byteLength"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(_engine, "get byteLength", ByteLength, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
                ["byteOffset"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(Engine, "get byteOffset", ByteOffset, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
                ["length"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(Engine, "get length", GetLength, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
                ["BYTES_PER_ELEMENT"] = new PropertyDescriptor(new PropertyDescriptor(_constructor._bytesPerElement, PropertyFlag.AllForbidden)),
                ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
                ["copyWithin"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "copyWithin", CopyWithin, 2, PropertyFlag.Configurable), propertyFlags),
                ["entries"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "entries", Entries, 0, PropertyFlag.Configurable), propertyFlags),
                ["every"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "every", Every, 1, PropertyFlag.Configurable), propertyFlags),
                ["fill"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "fill", Fill, 1, PropertyFlag.Configurable), propertyFlags),
                ["filter"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "filter", Filter, 1, PropertyFlag.Configurable), propertyFlags),
                ["find"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "find", Find, 1, PropertyFlag.Configurable), propertyFlags),
                ["findIndex"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "findIndex", FindIndex, 1, PropertyFlag.Configurable), propertyFlags),
                ["forEach"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "forEach", ForEach, 1, PropertyFlag.Configurable), propertyFlags),
                ["includes"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "includes", Includes, 1, PropertyFlag.Configurable), propertyFlags),
                ["indexOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "indexOf", IndexOf, 1, PropertyFlag.Configurable), propertyFlags),
                ["join"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "join", Join, 1, PropertyFlag.Configurable), propertyFlags),
                ["keys"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "keys", Keys, 0, PropertyFlag.Configurable), propertyFlags),
                ["lastIndexOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "lastIndexOf", LastIndexOf, 1, PropertyFlag.Configurable), propertyFlags),
                ["map"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "map", Map, 1, PropertyFlag.Configurable), propertyFlags),
                ["reduce"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "reduce", Reduce, 1, PropertyFlag.Configurable), propertyFlags),
                ["reduceRight"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "reduceRight", ReduceRight, 1, PropertyFlag.Configurable), propertyFlags),
                ["reverse"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "reverse", Reverse, 0, PropertyFlag.Configurable), propertyFlags),
                ["set"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "set", Set, 2, PropertyFlag.Configurable), propertyFlags),
                ["slice"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "slice", Slice, 2, PropertyFlag.Configurable), propertyFlags),
                ["some"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "some", Some, 1, PropertyFlag.Configurable), propertyFlags),
                ["sort"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "sort", Sort, 1, PropertyFlag.Configurable), propertyFlags),
                ["subarray"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "subarray", Subarray, 2, PropertyFlag.Configurable), propertyFlags),
                ["toLocaleString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString, 0, PropertyFlag.Configurable), propertyFlags),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleString", _realm.Intrinsics.Array.PrototypeObject.ToString, 0, PropertyFlag.Configurable), propertyFlags),
                ["values"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "values", Values, 0, PropertyFlag.Configurable), propertyFlags),
            };
            SetProperties(properties);

            _originalIteratorFunction = new ClrFunctionInstance(Engine, "iterator", Values, 1);
            var symbols = new SymbolDictionary(2)
            {
                [GlobalSymbolRegistry.Iterator] = new PropertyDescriptor(_originalIteratorFunction, propertyFlags),
                [GlobalSymbolRegistry.ToStringTag] = new GetSetPropertyDescriptor(
                    get: new ClrFunctionInstance(Engine, "get [Symbol.toStringTag]", ToStringTag, 0, PropertyFlag.Configurable),
                    set: Undefined,
                    PropertyFlag.Configurable)
            };
            SetSymbols(symbols);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-get-%typedarray%.prototype.buffer
        /// </summary>
        private JsValue Buffer(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj as TypedArrayInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            return o._viewedArrayBuffer;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-get-%typedarray%.prototype.bytelength
        /// </summary>
        private JsValue ByteLength(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj as TypedArrayInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            if (o._viewedArrayBuffer.IsDetachedBuffer)
            {
                return JsNumber.PositiveZero;
            }

            return JsNumber.Create(o._byteLength);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-get-%typedarray%.prototype.byteoffset
        /// </summary>
        private JsValue ByteOffset(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj as TypedArrayInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            if (o._viewedArrayBuffer.IsDetachedBuffer)
            {
                return JsNumber.PositiveZero;
            }

            return JsNumber.Create(o._byteOffset);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-get-%typedarray%.prototype.length
        /// </summary>
        private JsValue GetLength(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj as TypedArrayInstance;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            var buffer = o._viewedArrayBuffer;
            if (buffer.IsDetachedBuffer)
            {
                return JsNumber.PositiveZero;
            }

            return JsNumber.Create(Length);
        }

        private JsValue CopyWithin(JsValue thisObj, JsValue[] arguments)
        {
            throw new NotImplementedException("same as Array version");
        }

        private JsValue Entries(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);
            return CreateArrayIterator(o, ArrayIteratorType.KeyAndValue);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.every
        /// </summary>
        private JsValue Every(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);
            uint len = o.Length;

            if (len == 0)
            {
                return JsBoolean.True;
            }

            var predicate = GetCallable(arguments.At(0));
            var thisArg = arguments.At(1);

            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o;
            for (uint k = 0; k < len; k++)
            {
                args[0] = o[k];
                args[1] = k;
                if (!TypeConverter.ToBoolean(predicate.Call(thisArg, args)))
                {
                    return JsBoolean.False;
                }
            }
            _engine._jsValueArrayPool.ReturnArray(args);

            return JsBoolean.True;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.fill
        /// </summary>
        private JsValue Fill(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);

            var start = arguments.At(1);
            var end = arguments.At(1);

            var value = o._contentType == TypedArrayContentType.BigInt
                ? TypeConverter.ToBigInt(arguments.At(0))
                : TypeConverter.ToNumber(arguments.At(0));

            var len = o.Length;

            uint k;
            var relativeStart = TypeConverter.ToIntegerOrInfinity(start);
            if (double.IsNegativeInfinity(relativeStart))
            {
                k = 0;
            }
            else if (relativeStart < 0)
            {
                k = (uint) System.Math.Max(len + relativeStart, 0);
            }
            else
            {
                k = (uint) System.Math.Min(relativeStart, len);
            }

            uint final;
            var relativeEnd = end.IsUndefined() ? len : TypeConverter.ToIntegerOrInfinity(end);
            if (double.IsNegativeInfinity(relativeEnd))
            {
                final = 0;
            }
            else if (relativeEnd < 0)
            {
                final = (uint) System.Math.Max(len + relativeEnd, 0);
            }
            else
            {
                final = (uint) System.Math.Min(relativeEnd, len);
            }

            o._viewedArrayBuffer.AssertNotDetached();

            for (var i = k; i < final; ++i)
            {
                o[i] = value;
            }

            return thisObj;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.filter
        /// </summary>
        private JsValue Filter(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = GetCallable(arguments.At(0));
            var thisArg = arguments.At(1);

            var o = thisObj.ValidateTypedArray(_realm);
            var len = o.Length;

            var kept = new List<JsValue>();
            var captured = 0;

            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o;
            for (uint k = 0; k < len; k++)
            {
                var kValue = o[k];
                args[0] = kValue;
                args[1] = k;
                var selected = callbackfn.Call(thisArg, args);
                if (TypeConverter.ToBoolean(selected))
                {
                    kept.Add(kValue);
                    captured++;
                }
            }
            _engine._jsValueArrayPool.ReturnArray(args);

            var a = _realm.Intrinsics.TypedArray.TypedArraySpeciesCreate(o, new JsValue[] { captured });
            for (uint n = 0; n < captured; ++n)
            {
                a[n] = kept[(int) n];
            }
            return a;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.find
        /// </summary>
        private JsValue Find(JsValue thisObj, JsValue[] arguments)
        {
            return DoFind(thisObj, arguments).Value;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.findindex
        /// </summary>
        private JsValue FindIndex(JsValue thisObj, JsValue[] arguments)
        {
            return DoFind(thisObj, arguments).Key;
        }

        private KeyValuePair<JsValue, JsValue> DoFind(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);
            var len = o.Length;

            var predicate = GetCallable(arguments.At(0));
            var thisArg = arguments.At(1);

            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o;
            for (uint k = 0; k < len; k++)
            {
                var kNumber = JsNumber.Create(k);
                var kValue = o[k];
                args[0] = kValue;
                args[1] = kNumber;
                if (TypeConverter.ToBoolean(predicate.Call(thisArg, args)))
                {
                    return new KeyValuePair<JsValue, JsValue>(kNumber, kValue);
                }
            }

            return new KeyValuePair<JsValue, JsValue>(JsNumber.IntegerNegativeOne, Undefined);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.foreach
        /// </summary>
        private JsValue ForEach(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = GetCallable(arguments.At(0));
            var thisArg = arguments.At(1);

            var o = thisObj.ValidateTypedArray(_realm);
            var len = o.Length;

            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o;
            for (uint k = 0; k < len; k++)
            {
                var kValue = o[k];
                args[0] = kValue;
                args[1] = k;
                callbackfn.Call(thisArg, args);
            }
            _engine._jsValueArrayPool.ReturnArray(args);

            return Undefined;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.includes
        /// </summary>
        private JsValue Includes(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);
            var len = o.Length;

            if (len == 0)
            {
                return false;
            }

            var searchElement = arguments.At(0);
            var fromIndex = arguments.At(1, 0);

            var n = TypeConverter.ToIntegerOrInfinity(fromIndex);
            if (double.IsPositiveInfinity(n))
            {
                return JsBoolean.False;
            }
            else if (double.IsNegativeInfinity(n))
            {
                n = 0;
            }

            long k;
            if (n >= 0)
            {
                k = (long) n;
            }
            else
            {
                k = (long) (len + n);
                if (k < 0)
                {
                    k = 0;
                }
            }

            while (k < len)
            {
                var value = o[(uint) k];
                if (JintBinaryExpression.SameValueZero(value, searchElement))
                {
                    return JsBoolean.True;
                }
                k++;
            }
            return JsBoolean.False;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.indexof
        /// </summary>
        private JsValue IndexOf(JsValue thisObj, JsValue[] arguments)
        {
            var searchElement = arguments.At(0);
            var fromIndex = arguments.At(1);

            var o = thisObj.ValidateTypedArray(_realm);
            var len = o.Length;
            if (len == 0)
            {
                return JsNumber.IntegerNegativeOne;
            }

            var n = TypeConverter.ToIntegerOrInfinity(fromIndex);
            if (double.IsPositiveInfinity(n))
            {
                return JsNumber.IntegerNegativeOne;
            }
            else if (double.IsNegativeInfinity(n))
            {
                n = 0;
            }

            long k;
            if (n >= 0)
            {
                k = (long) n;
            }
            else
            {
                k = (long) (len + n);
                if (k < 0)
                {
                    k = 0;
                }
            }

            for (; k < len; k++)
            {
                var elementK = o[(uint) k];
                var same = JintBinaryExpression.StrictlyEqual(elementK, searchElement);
                if (same)
                {
                    return k;
                }
            }

            return JsNumber.IntegerNegativeOne;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.join
        /// </summary>
        private JsValue Join(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);

            var separator = arguments.At(0, JsString.CommaString);
            var len = o.Length;

            var sep = TypeConverter.ToString(separator);

            // as per the spec, this has to be called after ToString(separator)
            if (len == 0)
            {
                return JsString.Empty;
            }

            static string StringFromJsValue(JsValue value)
            {
                return value.IsUndefined()
                    ? ""
                    : TypeConverter.ToString(value);
            }

            var s = StringFromJsValue(o[0]);
            if (len == 1)
            {
                return s;
            }

            using var sb = StringBuilderPool.Rent();
            sb.Builder.Append(s);
            for (uint k = 1; k < len; k++)
            {
                sb.Builder.Append(sep);
                sb.Builder.Append(StringFromJsValue(o[k]));
            }

            return sb.ToString();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.keys
        /// </summary>
        private JsValue Keys(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);
            return CreateArrayIterator(o, ArrayIteratorType.Key);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.lastindexof
        /// </summary>
        private JsValue LastIndexOf(JsValue thisObj, JsValue[] arguments)
        {
            var searchElement = arguments.At(0);

            var o = thisObj.ValidateTypedArray(_realm);
            var len = o.Length;
            if (len == 0)
            {
                return JsNumber.IntegerNegativeOne;
            }

            var fromIndex = arguments.At(1, len - 1);
            var n = TypeConverter.ToIntegerOrInfinity(fromIndex);

            if (double.IsNegativeInfinity(n))
            {
                return JsNumber.IntegerNegativeOne;
            }

            double k;
            if (n >= 0)
            {
                k = System.Math.Min(n, len - 1);
            }
            else
            {
                k = len + n;
            }

            for (; k >= 0; k--)
            {
                var elementK = o[(uint) k];
                var same = JintBinaryExpression.StrictlyEqual(elementK, searchElement);
                if (same)
                {
                    return k;
                }
            }

            return JsNumber.IntegerNegativeOne;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.map
        /// </summary>
        private ObjectInstance Map(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);
            var len = o.Length;

            var thisArg = arguments.At(1);
            var callable = GetCallable(arguments.At(0));

            var a = _realm.Intrinsics.TypedArray.TypedArraySpeciesCreate(o, new JsValue[] { len });
            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o;
            for (uint k = 0; k < len; k++)
            {
                args[0] = o[k];
                args[1] = k;
                var mappedValue = callable.Call(thisArg, args);
                a[k] = mappedValue;
            }
            _engine._jsValueArrayPool.ReturnArray(args);
            return a;
        }

        private ObjectInstance Reduce(JsValue thisObj, JsValue[] arguments)
        {
            throw new NotImplementedException("same as Array version");
        }

        private ObjectInstance ReduceRight(JsValue thisObj, JsValue[] arguments)
        {
            throw new NotImplementedException("same as Array version");
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.reverse
        /// </summary>
        private ObjectInstance Reverse(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);
            var len = o.Length;
            var middle = (ulong) System.Math.Floor(len / 2.0);
            uint lower = 0;
            while (lower != middle)
            {
                var upper = len - lower - 1;

                var lowerValue = o[lower];
                var upperValue = o[upper];

                o[lower] = upperValue;
                o[upper] = lowerValue;

                lower++;
            }

            return o;
        }

        private ObjectInstance Set(JsValue thisObj, JsValue[] arguments)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.slice
        /// </summary>
        private JsValue Slice(JsValue thisObj, JsValue[] arguments)
        {
                        var start = arguments.At(0);
            var end = arguments.At(1);

            var o = thisObj.ValidateTypedArray(_realm);
            var len = o.Length;

            var relativeStart = TypeConverter.ToIntegerOrInfinity(start);
            int k;
            if (double.IsNegativeInfinity(relativeStart))
            {
                k = 0;
            }
            else if (relativeStart < 0)
            {
                k = (int) System.Math.Max(len + relativeStart, 0);
            }
            else
            {
                k = (int) System.Math.Min(relativeStart, len);
            }

            double relativeEnd;
            if (end.IsUndefined())
            {
                relativeEnd = len;
            }
            else
            {
                relativeEnd = TypeConverter.ToIntegerOrInfinity(end);
            }

            int final;
            if (double.IsNegativeInfinity(relativeEnd))
            {
                final = (int) System.Math.Max(len + relativeEnd, 0);
            }
            else if (relativeEnd < 0)
            {
                final = (int) System.Math.Max(len + relativeEnd, 0);
            }
            else
            {
                final = (int) System.Math.Min(TypeConverter.ToInteger(relativeEnd), len);
            }

            var count = System.Math.Max(final - k, 0);
            var length = (uint) System.Math.Max(0, relativeEnd - k);
            var a = _realm.Intrinsics.TypedArray.TypedArraySpeciesCreate(o, new JsValue[] { length });


            if (count > 0)
            {
                o._viewedArrayBuffer.AssertNotDetached();
                var srcType = o._arrayElementType;
                var targetType = a._arrayElementType;
                if (srcType != targetType)
                {
                    var n = 0;
                    while (k < final)
                    {
                        var kValue = o[(uint) k];
                        a[(uint) n] = kValue;
                        k++;
                        n++;
                    }
                }
                else
                {
                    var srcBuffer = o._viewedArrayBuffer;
                    var targetBuffer = a._viewedArrayBuffer;
                    var elementSize = srcType.GetElementSize();
                    var srcByteOffset = o._byteOffset;
                    var targetByteIndex = a._byteOffset;
                    var srcByteIndex = (uint) (k * elementSize + srcByteOffset);
                    var limit = targetByteIndex + count * elementSize;
                    while (targetByteIndex < limit)
                    {
                        var value = (JsNumber) srcBuffer.GetValueFromBuffer(srcByteIndex, TypedArrayElementType.Uint8, true, ArrayBufferOrder.Unordered);
                        targetBuffer.SetValueInBuffer(targetByteIndex, TypedArrayElementType.Uint8, value._value, true, ArrayBufferOrder.Unordered);
                        srcByteIndex++;
                        targetByteIndex++;
                    }
                }
            }
            return a;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.some
        /// </summary>
        private JsValue Some(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);
            var len = o.Length;
            var callbackfn = GetCallable(arguments.At(0));
            var thisArg = arguments.At(1);

            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o;
            for (uint k = 0; k < len; k++)
            {
                args[0] = o[k];
                args[1] = k;
                if (TypeConverter.ToBoolean(callbackfn.Call(thisArg, args)))
                {
                    return JsBoolean.True;
                }
            }
            _engine._jsValueArrayPool.ReturnArray(args);
            return JsBoolean.False;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.sort
        /// </summary>
        private JsValue Sort(JsValue thisObj, JsValue[] arguments)
        {
            /*
             * %TypedArray%.prototype.sort is a distinct function that, except as described below,
             * implements the same requirements as those of Array.prototype.sort as defined in 23.1.3.27.
             * The implementation of the %TypedArray%.prototype.sort specification may be optimized with the knowledge that the this value is
             * an object that has a fixed length and whose integer-indexed properties are not sparse.
             */

            var obj = thisObj.ValidateTypedArray(_realm);
            var buffer = obj._viewedArrayBuffer;
            var len = obj.Length;

            var compareArg = arguments.At(0);
            ICallable compareFn = null;
            if (!compareArg.IsUndefined())
            {
                compareFn = GetCallable(compareArg);
            }

            if (len <= 1)
            {
                return obj;
            }

            var array = obj.OrderBy(x => x, TypedArrayComparer.WithFunction(buffer, compareFn)).ToArray();

            for (uint i = 0; i < (uint) array.Length; ++i)
            {
                obj[i] = array[i];
            }

            return obj;
        }

       /// <summary>
       /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.subarray
       /// </summary>
       private JsValue Subarray(JsValue thisObj, JsValue[] arguments)
       {
           var o = thisObj as TypedArrayInstance;
           if (o is null)
           {
               ExceptionHelper.ThrowTypeError(_realm);
           }

           var begin = arguments.At(0);
           var end = arguments.At(1);

           var buffer = o._viewedArrayBuffer;
           var srcLength = o.Length;
           var relativeBegin = TypeConverter.ToIntegerOrInfinity(begin);

           double beginIndex;
           if (double.IsNegativeInfinity(relativeBegin))
           {
               beginIndex = 0;
           }
           else if (relativeBegin < 0)
           {
               beginIndex = System.Math.Max(srcLength + relativeBegin, 0);
           }
           else
           {
               beginIndex = System.Math.Min(relativeBegin, srcLength);
           }

           double relativeEnd;
           if (end.IsUndefined())
           {
               relativeEnd = srcLength;
           }
           else
           {
               relativeEnd = TypeConverter.ToIntegerOrInfinity(end);
           }

           double endIndex;
           if (double.IsNegativeInfinity(relativeEnd))
           {
               endIndex = 0;
           }
           else if (relativeEnd < 0)
           {
               endIndex = System.Math.Max(srcLength + relativeEnd, 0);
           }
           else
           {
               endIndex = System.Math.Min(relativeEnd, srcLength);
           }

           var newLength = System.Math.Max(endIndex - beginIndex, 0);
           var elementSize = o._arrayElementType.GetElementSize();
           var srcByteOffset = o._byteOffset;
           var beginByteOffset = srcByteOffset + beginIndex * elementSize;
           var argumentsList = new JsValue[] { buffer, beginByteOffset, newLength };
           return _realm.Intrinsics.TypedArray.TypedArraySpeciesCreate(o, argumentsList);
       }

       /// <summary>
       /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.tolocalestring
       /// </summary>
       private JsValue ToLocaleString(JsValue thisObj, JsValue[] arguments)
       {
           /*
            * %TypedArray%.prototype.toLocaleString is a distinct function that implements the same algorithm as Array.prototype.toLocaleString
            * as defined in 23.1.3.29 except that the this value's [[ArrayLength]] internal slot is accessed in place of performing
            * a [[Get]] of "length". The implementation of the algorithm may be optimized with the knowledge that the this value is an object
            * that has a fixed length and whose integer-indexed properties are not sparse. However, such optimization must not introduce
            * any observable changes in the specified behaviour of the algorithm.
            */

           var array = thisObj.ValidateTypedArray(_realm);
           var len = array.Length;
           const string separator = ",";
           if (len == 0)
           {
               return JsString.Empty;
           }

           JsValue r;
           if (!array.TryGetValue(0, out var firstElement) || firstElement.IsNull() || firstElement.IsUndefined())
           {
               r = JsString.Empty;
           }
           else
           {
               var elementObj = TypeConverter.ToObject(_realm, firstElement);
               var func = elementObj.Get("toLocaleString", elementObj) as ICallable;
               if (func is null)
               {
                   ExceptionHelper.ThrowTypeError(_realm);
               }

               r = func.Call(elementObj, Arguments.Empty);
           }

           for (uint k = 1; k < len; k++)
           {
               string s = r + separator;
               var elementObj = TypeConverter.ToObject(_realm, array[k]);
               var func = elementObj.Get("toLocaleString", elementObj) as ICallable;
               if (func is null)
               {
                   ExceptionHelper.ThrowTypeError(_realm);
               }

               r = func.Call(elementObj, Arguments.Empty);

               r = s + r;
           }

           return r;

       }

       /// <summary>
       /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.values
       /// </summary>
       private JsValue Values(JsValue thisObj, JsValue[] arguments)
       {
           var o = thisObj.ValidateTypedArray(_realm);
           return CreateArrayIterator(o, ArrayIteratorType.Value);
       }

       /// <summary>
       /// https://tc39.es/ecma262/#sec-get-%typedarray%.prototype-@@tostringtag
       /// </summary>
       private static JsValue ToStringTag(JsValue thisObj, JsValue[] arguments)
       {
           if (thisObj is not TypedArrayInstance o)
           {
               return Undefined;
           }

           return o._arrayElementType.GetTypedArrayName();
       }

       private IteratorInstance CreateArrayIterator(ObjectInstance array, ArrayIteratorType kind)
       {
           return kind switch
           {
               ArrayIteratorType.Key => _realm.Intrinsics.Iterator.ConstructArrayLikeKeyIterator(array),
               ArrayIteratorType.Value => _realm.Intrinsics.Iterator.ConstructArrayLikeValueIterator(array),
               _ => _realm.Intrinsics.Iterator.ConstructArrayLikeEntriesIterator(array)
           };
       }

       private enum ArrayIteratorType
       {
           Key,
           Value,
           KeyAndValue
       }

        private sealed class TypedArrayComparer : IComparer<JsValue>
        {
            public static TypedArrayComparer WithFunction(ArrayBufferInstance buffer, ICallable compare)
            {
                return new TypedArrayComparer(buffer, compare);
            }

            private readonly ArrayBufferInstance _buffer;
            private readonly ICallable _compare;
            private readonly JsValue[] _comparableArray = new JsValue[2];

            private TypedArrayComparer(ArrayBufferInstance buffer, ICallable compare)
            {
                _buffer = buffer;
                _compare = compare;
            }

            public int Compare(JsValue x, JsValue y)
            {
                if (_compare != null)
                {
                    _comparableArray[0] = x;
                    _comparableArray[1] = y;

                    var v = TypeConverter.ToNumber(_compare.Call(Undefined, _comparableArray));
                    _buffer.AssertNotDetached();

                    if (double.IsNaN(v))
                    {
                        return 0;
                    }

                    return (int) v;
                }

                var xValue = x.AsNumber();
                var yValue = y.AsNumber();

                if (double.IsNaN(xValue) && double.IsNaN(yValue))
                {
                    return 0;
                }

                if (double.IsNaN(xValue))
                {
                    return 1;

                }
                if (double.IsNaN(yValue))
                {
                    return -1;
                }

                if (xValue < yValue)
                {
                    return -1;
                }

                if (xValue > yValue)
                {
                    return 1;
                }

                if (NumberInstance.IsNegativeZero(xValue) && yValue == 0)
                {
                    return -1;
                }

                if (xValue == 0 && NumberInstance.IsNegativeZero(yValue))
                {
                    return 1;
                }

                return 0;
            }
        }

    }
}