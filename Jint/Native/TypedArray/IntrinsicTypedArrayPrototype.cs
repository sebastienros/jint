using System;
using System.Collections.Generic;
using System.Linq;
using Jint.Collections;
using Jint.Native.Array;
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
    internal sealed class IntrinsicTypedArrayPrototype : ObjectInstance
    {
        private readonly Realm _realm;
        private readonly IntrinsicTypedArrayConstructor _constructor;
        private ClrFunctionInstance _originalIteratorFunction;

        internal IntrinsicTypedArrayPrototype(
            Engine engine,
            Realm realm,
            ObjectInstance objectPrototype,
            IntrinsicTypedArrayConstructor constructor) : base(engine)
        {
            _prototype = objectPrototype;
            _realm = realm;
            _constructor = constructor;
        }

        protected override void Initialize()
        {
            const PropertyFlag lengthFlags = PropertyFlag.Configurable;
            const PropertyFlag propertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
            var properties = new PropertyDictionary(31, false)
            {
                ["buffer"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(_engine, "get buffer", Buffer, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
                ["byteLength"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(_engine, "get byteLength", ByteLength, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
                ["byteOffset"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(Engine, "get byteOffset", ByteOffset, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
                ["length"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(Engine, "get length", GetLength, 0, lengthFlags), Undefined, PropertyFlag.Configurable),
                ["constructor"] = new(_constructor, PropertyFlag.NonEnumerable),
                ["copyWithin"] = new(new ClrFunctionInstance(Engine, "copyWithin", CopyWithin, 2, PropertyFlag.Configurable), propertyFlags),
                ["entries"] = new(new ClrFunctionInstance(Engine, "entries", Entries, 0, PropertyFlag.Configurable), propertyFlags),
                ["every"] = new(new ClrFunctionInstance(Engine, "every", Every, 1, PropertyFlag.Configurable), propertyFlags),
                ["fill"] = new(new ClrFunctionInstance(Engine, "fill", Fill, 1, PropertyFlag.Configurable), propertyFlags),
                ["filter"] = new(new ClrFunctionInstance(Engine, "filter", Filter, 1, PropertyFlag.Configurable), propertyFlags),
                ["find"] = new(new ClrFunctionInstance(Engine, "find", Find, 1, PropertyFlag.Configurable), propertyFlags),
                ["findIndex"] = new(new ClrFunctionInstance(Engine, "findIndex", FindIndex, 1, PropertyFlag.Configurable), propertyFlags),
                ["forEach"] = new(new ClrFunctionInstance(Engine, "forEach", ForEach, 1, PropertyFlag.Configurable), propertyFlags),
                ["includes"] = new(new ClrFunctionInstance(Engine, "includes", Includes, 1, PropertyFlag.Configurable), propertyFlags),
                ["indexOf"] = new(new ClrFunctionInstance(Engine, "indexOf", IndexOf, 1, PropertyFlag.Configurable), propertyFlags),
                ["join"] = new(new ClrFunctionInstance(Engine, "join", Join, 1, PropertyFlag.Configurable), propertyFlags),
                ["keys"] = new(new ClrFunctionInstance(Engine, "keys", Keys, 0, PropertyFlag.Configurable), propertyFlags),
                ["lastIndexOf"] = new(new ClrFunctionInstance(Engine, "lastIndexOf", LastIndexOf, 1, PropertyFlag.Configurable), propertyFlags),
                ["map"] = new(new ClrFunctionInstance(Engine, "map", Map, 1, PropertyFlag.Configurable), propertyFlags),
                ["reduce"] = new(new ClrFunctionInstance(Engine, "reduce", Reduce, 1, PropertyFlag.Configurable), propertyFlags),
                ["reduceRight"] = new(new ClrFunctionInstance(Engine, "reduceRight", ReduceRight, 1, PropertyFlag.Configurable), propertyFlags),
                ["reverse"] = new(new ClrFunctionInstance(Engine, "reverse", Reverse, 0, PropertyFlag.Configurable), propertyFlags),
                ["set"] = new(new ClrFunctionInstance(Engine, "set", Set, 1, PropertyFlag.Configurable), propertyFlags),
                ["slice"] = new(new ClrFunctionInstance(Engine, "slice", Slice, 2, PropertyFlag.Configurable), propertyFlags),
                ["some"] = new(new ClrFunctionInstance(Engine, "some", Some, 1, PropertyFlag.Configurable), propertyFlags),
                ["sort"] = new(new ClrFunctionInstance(Engine, "sort", Sort, 1, PropertyFlag.Configurable), propertyFlags),
                ["subarray"] = new(new ClrFunctionInstance(Engine, "subarray", Subarray, 2, PropertyFlag.Configurable), propertyFlags),
                ["toLocaleString"] = new(new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString, 0, PropertyFlag.Configurable), propertyFlags),
                ["toString"] = new(new ClrFunctionInstance(Engine, "toLocaleString", _realm.Intrinsics.Array.PrototypeObject.ToString, 0, PropertyFlag.Configurable), propertyFlags),
                ["values"] = new(new ClrFunctionInstance(Engine, "values", Values, 0, PropertyFlag.Configurable), propertyFlags),
                ["at"] = new(new ClrFunctionInstance(Engine, "at", At, 1, PropertyFlag.Configurable), propertyFlags),
            };
            SetProperties(properties);

            _originalIteratorFunction = new ClrFunctionInstance(Engine, "iterator", Values, 1);
            var symbols = new SymbolDictionary(2)
            {
                [GlobalSymbolRegistry.Iterator] = new(_originalIteratorFunction, propertyFlags),
                [GlobalSymbolRegistry.ToStringTag] = new GetSetPropertyDescriptor(
                    new ClrFunctionInstance(Engine, "get [Symbol.toStringTag]", ToStringTag, 0, PropertyFlag.Configurable),
                    Undefined,
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

            return JsNumber.Create(o.Length);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.copywithin
        /// </summary>
        private JsValue CopyWithin(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);

            var target = arguments.At(0);
            var start = arguments.At(1);
            var end = arguments.At(2);

            long len = o.Length;

            var relativeTarget = TypeConverter.ToIntegerOrInfinity(target);

            long to;
            if (double.IsNegativeInfinity(relativeTarget))
            {
                to = 0;
            }
            else if (relativeTarget < 0)
            {
                to = (long) System.Math.Max(len + relativeTarget, 0);
            }
            else
            {
                to = (long) System.Math.Min(relativeTarget, len);
            }

            var relativeStart = TypeConverter.ToIntegerOrInfinity(start);

            long from;
            if (double.IsNegativeInfinity(relativeStart))
            {
                from = 0;
            }
            else if (relativeStart < 0)
            {
                from = (long) System.Math.Max(len + relativeStart, 0);
            }
            else
            {
                from = (long) System.Math.Min(relativeStart, len);
            }

            var relativeEnd = end.IsUndefined()
                ? len
                : TypeConverter.ToIntegerOrInfinity(end);

            long final;
            if (double.IsNegativeInfinity(relativeEnd))
            {
                final = 0;
            }
            else if (relativeEnd < 0)
            {
                final = (long) System.Math.Max(len + relativeEnd, 0);
            }
            else
            {
                final = (long) System.Math.Min(relativeEnd, len);
            }

            var count = System.Math.Min(final - from, len - to);

            if (count > 0)
            {
                var buffer = o._viewedArrayBuffer;
                buffer.AssertNotDetached();

                var elementSize = o._arrayElementType.GetElementSize();
                var byteOffset = o._byteOffset;
                var toByteIndex = to * elementSize + byteOffset;
                var fromByteIndex = from * elementSize + byteOffset;
                var countBytes = count * elementSize;

                int direction;
                if (fromByteIndex < toByteIndex && toByteIndex < fromByteIndex + countBytes)
                {
                    direction = -1;
                    fromByteIndex = fromByteIndex + countBytes - 1;
                    toByteIndex = toByteIndex + countBytes - 1;
                }
                else
                {
                    direction = 1;
                }

                while (countBytes > 0)
                {
                    var value = buffer.GetValueFromBuffer((int) fromByteIndex, TypedArrayElementType.Uint8, true, ArrayBufferOrder.Unordered);
                    buffer.SetValueInBuffer((int) toByteIndex, TypedArrayElementType.Uint8, value, true, ArrayBufferOrder.Unordered);
                    fromByteIndex += direction;
                    toByteIndex += direction;
                    countBytes--;
                }
            }

            return o;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.entries
        /// </summary>
        private JsValue Entries(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);
            return _realm.Intrinsics.ArrayIteratorPrototype.Construct(o, ArrayIteratorType.KeyAndValue);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.every
        /// </summary>
        private JsValue Every(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);
            var len = o.Length;

            if (len == 0)
            {
                return JsBoolean.True;
            }

            var predicate = GetCallable(arguments.At(0));
            var thisArg = arguments.At(1);

            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o;
            for (var k = 0; k < len; k++)
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
            var end = arguments.At(2);

            var value = o._contentType == TypedArrayContentType.BigInt
                ? TypeConverter.ToBigInt(arguments.At(0))
                : TypeConverter.ToNumber(arguments.At(0));

            var len = o.Length;

            int k;
            var relativeStart = TypeConverter.ToIntegerOrInfinity(start);
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
            for (var k = 0; k < len; k++)
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
            for (var n = 0; n < captured; ++n)
            {
                a[n] = kept[n];
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
            for (var k = 0; k < len; k++)
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
            for (var k = 0; k < len; k++)
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
                var value = o[(int) k];
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
                var kPresent = o.HasProperty(k);
                if (kPresent)
                {
                    var elementK = o[(int) k];
                    var same = JintBinaryExpression.StrictlyEqual(elementK, searchElement);
                    if (same)
                    {
                        return k;
                    }
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

            var separator = arguments.At(0);
            var len = o.Length;

            var sep = TypeConverter.ToString(separator.IsUndefined() ? JsString.CommaString : separator);
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
            for (var k = 1; k < len; k++)
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
            return _realm.Intrinsics.ArrayIteratorPrototype.Construct(o, ArrayIteratorType.Key);
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

            long k;
            if (n >= 0)
            {
                k = (long) System.Math.Min(n, len - 1);
            }
            else
            {
                k = (long) (len + n);
            }

            for (; k >= 0; k--)
            {
                var kPresent = o.HasProperty(k);
                if (kPresent)
                {
                    var elementK = o[(int) k];
                    var same = JintBinaryExpression.StrictlyEqual(elementK, searchElement);
                    if (same)
                    {
                        return k;
                    }
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
            for (var k = 0; k < len; k++)
            {
                args[0] = o[k];
                args[1] = k;
                var mappedValue = callable.Call(thisArg, args);
                a[k] = mappedValue;
            }

            _engine._jsValueArrayPool.ReturnArray(args);
            return a;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.reduce
        /// </summary>
        private JsValue Reduce(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = GetCallable(arguments.At(0));
            var initialValue = arguments.At(1);

            var o = thisObj.ValidateTypedArray(_realm);
            var len = o.Length;

            if (len == 0 && arguments.Length < 2)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            var k = 0;
            var accumulator = Undefined;
            if (!initialValue.IsUndefined())
            {
                accumulator = initialValue;
            }
            else
            {
                accumulator = o[k];
                k++;
            }

            var args = _engine._jsValueArrayPool.RentArray(4);
            args[3] = o;
            while (k < len)
            {
                var kValue = o[k];
                args[0] = accumulator;
                args[1] = kValue;
                args[2] = k;
                accumulator = callbackfn.Call(Undefined, args);
                k++;
            }

            _engine._jsValueArrayPool.ReturnArray(args);

            return accumulator;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.reduceright
        /// </summary>
        private JsValue ReduceRight(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = GetCallable(arguments.At(0));
            var initialValue = arguments.At(1);

            var o = thisObj.ValidateTypedArray(_realm);
            var len = (int) o.Length;

            if (len == 0 && arguments.Length < 2)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            var k = len - 1;
            JsValue accumulator;
            if (arguments.Length > 1)
            {
                accumulator = initialValue;
            }
            else
            {
                accumulator = o[k];
                k--;
            }

            var jsValues = _engine._jsValueArrayPool.RentArray(4);
            jsValues[3] = o;
            for (; k >= 0; k--)
            {
                jsValues[0] = accumulator;
                jsValues[1] = o[k];
                jsValues[2] = k;
                accumulator = callbackfn.Call(Undefined, jsValues);
            }

            _engine._jsValueArrayPool.ReturnArray(jsValues);
            return accumulator;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.reverse
        /// </summary>
        private ObjectInstance Reverse(JsValue thisObj, JsValue[] arguments)
        {
            var o = thisObj.ValidateTypedArray(_realm);
            var len = (int) o.Length;
            var middle = (int) System.Math.Floor(len / 2.0);
            var lower = 0;
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

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.set
        /// </summary>
        private JsValue Set(JsValue thisObj, JsValue[] arguments)
        {
            var target = thisObj as TypedArrayInstance;
            if (target is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            var source = arguments.At(0);
            var offset = arguments.At(1);

            var targetOffset = TypeConverter.ToIntegerOrInfinity(offset);
            if (targetOffset < 0)
            {
                ExceptionHelper.ThrowRangeError(_realm, "Invalid offset");
            }

            if (source is TypedArrayInstance typedArrayInstance)
            {
                SetTypedArrayFromTypedArray(target, targetOffset, typedArrayInstance);
            }
            else
            {
                SetTypedArrayFromArrayLike(target, targetOffset, source);
            }

            return Undefined;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-settypedarrayfromtypedarray
        /// </summary>
        private void SetTypedArrayFromTypedArray(TypedArrayInstance target, double targetOffset, TypedArrayInstance source)
        {
            var targetBuffer = target._viewedArrayBuffer;
            targetBuffer.AssertNotDetached();

            var targetLength = target._arrayLength;
            var srcBuffer = source._viewedArrayBuffer;
            srcBuffer.AssertNotDetached();

            var targetType = target._arrayElementType;
            var targetElementSize = targetType.GetElementSize();
            var targetByteOffset = target._byteOffset;

            var srcType = source._arrayElementType;
            var srcElementSize = srcType.GetElementSize();
            var srcLength = source._arrayLength;
            var srcByteOffset = source._byteOffset;

            if (double.IsNegativeInfinity(targetOffset))
            {
                ExceptionHelper.ThrowRangeError(_realm, "Invalid target offset");
            }

            if (srcLength + targetOffset > targetLength)
            {
                ExceptionHelper.ThrowRangeError(_realm, "Invalid target offset");
            }

            if (target._contentType != source._contentType)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Content type mismatch");
            }

            bool same;
            if (srcBuffer.IsSharedArrayBuffer && targetBuffer.IsSharedArrayBuffer)
            {
                // a. If srcBuffer.[[ArrayBufferData]] and targetBuffer.[[ArrayBufferData]] are the same Shared Data Block values, let same be true; else let same be false.
                ExceptionHelper.ThrowNotImplementedException("SharedBuffer not implemented");
                same = false;
            }
            else
            {
                same = SameValue(srcBuffer, targetBuffer);
            }

            int srcByteIndex;
            if (same)
            {
                var srcByteLength = source._byteLength;
                srcBuffer = srcBuffer.CloneArrayBuffer(_realm.Intrinsics.ArrayBuffer, srcByteOffset, srcByteLength, _realm.Intrinsics.ArrayBuffer);
                // %ArrayBuffer% is used to clone srcBuffer because is it known to not have any observable side-effects.
                srcByteIndex = 0;
            }
            else
            {
                srcByteIndex = srcByteOffset;
            }

            var targetByteIndex = (int) (targetOffset * targetElementSize + targetByteOffset);
            var limit = targetByteIndex + targetElementSize * srcLength;

            if (srcType == targetType)
            {
                // NOTE: If srcType and targetType are the same, the transfer must be performed in a manner that preserves the bit-level encoding of the source data.
                while (targetByteIndex < limit)
                {
                    var value = srcBuffer.GetValueFromBuffer(srcByteIndex, TypedArrayElementType.Uint8, true, ArrayBufferOrder.Unordered);
                    targetBuffer.SetValueInBuffer(targetByteIndex, TypedArrayElementType.Uint8, value, true, ArrayBufferOrder.Unordered);
                    srcByteIndex += 1;
                    targetByteIndex += 1;
                }
            }
            else
            {
                while (targetByteIndex < limit)
                {
                    var value = srcBuffer.GetValueFromBuffer(srcByteIndex, srcType, true, ArrayBufferOrder.Unordered);
                    targetBuffer.SetValueInBuffer(targetByteIndex, targetType, value, true, ArrayBufferOrder.Unordered);
                    srcByteIndex += srcElementSize;
                    targetByteIndex += targetElementSize;
                }
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-settypedarrayfromarraylike
        /// </summary>
        private void SetTypedArrayFromArrayLike(TypedArrayInstance target, double targetOffset, JsValue source)
        {
            var targetBuffer = target._viewedArrayBuffer;
            targetBuffer.AssertNotDetached();

            var targetLength = target._arrayLength;
            var targetElementSize = target._arrayElementType.GetElementSize();
            var targetType = target._arrayElementType;
            var targetByteOffset = target._byteOffset;
            var src = ArrayOperations.For(TypeConverter.ToObject(_realm, source));
            var srcLength = src.GetLength();

            if (double.IsNegativeInfinity(targetOffset))
            {
                ExceptionHelper.ThrowRangeError(_realm, "Invalid target offset");
            }

            if (srcLength + targetOffset > targetLength)
            {
                ExceptionHelper.ThrowRangeError(_realm, "Invalid target offset");
            }

            var targetByteIndex = targetOffset * targetElementSize + targetByteOffset;
            ulong k = 0;
            var limit = targetByteIndex + targetElementSize * srcLength;

            while (targetByteIndex < limit)
            {
                double value;
                if (target._contentType == TypedArrayContentType.BigInt)
                {
                    value = TypeConverter.ToBigInt(src.Get(k));
                }
                else
                {
                    value = TypeConverter.ToNumber(src.Get(k));
                }

                targetBuffer.AssertNotDetached();
                targetBuffer.SetValueInBuffer((int) targetByteIndex, targetType, value, true, ArrayBufferOrder.Unordered);
                k++;
                targetByteIndex += targetElementSize;
            }
        }

        /// <summary>
        /// https://tc39.es/proposal-relative-indexing-method/#sec-%typedarray.prototype%-additions
        /// </summary>
        private JsValue At(JsValue thisObj, JsValue[] arguments)
        {
            var start = arguments.At(0);

            var o = thisObj.ValidateTypedArray(_realm);
            long len = o.Length;

            var relativeStart = TypeConverter.ToInteger(start);
            int k;

            if (relativeStart < 0)
            {
                k = (int) (len + relativeStart);
            }
            else
            {
                k = (int) relativeStart;
            }

            if(k < 0 || k >= len)
            {
                return Undefined;
            }

            return o.Get(k);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.slice
        /// </summary>
        private JsValue Slice(JsValue thisObj, JsValue[] arguments)
        {
            var start = arguments.At(0);
            var end = arguments.At(1);

            var o = thisObj.ValidateTypedArray(_realm);
            long len = o.Length;

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

            var relativeEnd = end.IsUndefined()
                ? len
                : TypeConverter.ToIntegerOrInfinity(end);

            long final;
            if (double.IsNegativeInfinity(relativeEnd))
            {
                final = 0;
            }
            else if (relativeEnd < 0)
            {
                final = (long) System.Math.Max(len + relativeEnd, 0);
            }
            else
            {
                final = (long) System.Math.Min(relativeEnd, len);
            }

            var count = System.Math.Max(final - k, 0);
            var a = _realm.Intrinsics.TypedArray.TypedArraySpeciesCreate(o, new JsValue[] { count });

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
                        var kValue = o[k];
                        a[n] = kValue;
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
                    var srcByteIndex = (int) k * elementSize + srcByteOffset;
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
            for (var k = 0; k < len; k++)
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

            JsValue[] array;
            try
            {
                var comparer = TypedArrayComparer.WithFunction(buffer, compareFn);
                var operations = ArrayOperations.For(obj);
                array = operations
                    .OrderBy(x => x, comparer)
                    .ToArray();
            }
            catch (InvalidOperationException e)
            {
                throw e.InnerException ?? e;
            }

            for (var i = 0; i < (uint) array.Length; ++i)
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

            for (var k = 1; k < len; k++)
            {
                var s = r + separator;
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
            return _realm.Intrinsics.ArrayIteratorPrototype.Construct(o, ArrayIteratorType.Value);
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
                if (_compare is not null)
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