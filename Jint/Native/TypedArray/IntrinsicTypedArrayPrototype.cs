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

namespace Jint.Native.TypedArray
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-properties-of-the-%typedarrayprototype%-object
    /// </summary>
    internal sealed class IntrinsicTypedArrayPrototype : Prototype
    {
        private readonly IntrinsicTypedArrayConstructor _constructor;
        private ClrFunctionInstance? _originalIteratorFunction;

        internal IntrinsicTypedArrayPrototype(
            Engine engine,
            ObjectInstance objectPrototype,
            IntrinsicTypedArrayConstructor constructor) : base(engine, engine.Realm)
        {
            _prototype = objectPrototype;
            _constructor = constructor;
        }

        protected override void Initialize()
        {
            const PropertyFlag LengthFlags = PropertyFlag.Configurable;
            const PropertyFlag PropertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
            var properties = new PropertyDictionary(36, false)
            {
                ["at"] = new(new ClrFunctionInstance(Engine, "at", At, 1, PropertyFlag.Configurable), PropertyFlags),
                ["buffer"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(_engine, "get buffer", Buffer, 0, LengthFlags), Undefined, PropertyFlag.Configurable),
                ["byteLength"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(_engine, "get byteLength", ByteLength, 0, LengthFlags), Undefined, PropertyFlag.Configurable),
                ["byteOffset"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(Engine, "get byteOffset", ByteOffset, 0, LengthFlags), Undefined, PropertyFlag.Configurable),
                ["constructor"] = new(_constructor, PropertyFlag.NonEnumerable),
                ["copyWithin"] = new(new ClrFunctionInstance(Engine, "copyWithin", CopyWithin, 2, PropertyFlag.Configurable), PropertyFlags),
                ["entries"] = new(new ClrFunctionInstance(Engine, "entries", Entries, 0, PropertyFlag.Configurable), PropertyFlags),
                ["every"] = new(new ClrFunctionInstance(Engine, "every", Every, 1, PropertyFlag.Configurable), PropertyFlags),
                ["fill"] = new(new ClrFunctionInstance(Engine, "fill", Fill, 1, PropertyFlag.Configurable), PropertyFlags),
                ["filter"] = new(new ClrFunctionInstance(Engine, "filter", Filter, 1, PropertyFlag.Configurable), PropertyFlags),
                ["find"] = new(new ClrFunctionInstance(Engine, "find", Find, 1, PropertyFlag.Configurable), PropertyFlags),
                ["findIndex"] = new(new ClrFunctionInstance(Engine, "findIndex", FindIndex, 1, PropertyFlag.Configurable), PropertyFlags),
                ["findLast"] = new(new ClrFunctionInstance(Engine, "findLast", FindLast, 1, PropertyFlag.Configurable), PropertyFlags),
                ["findLastIndex"] = new(new ClrFunctionInstance(Engine, "findLastIndex", FindLastIndex, 1, PropertyFlag.Configurable), PropertyFlags),
                ["forEach"] = new(new ClrFunctionInstance(Engine, "forEach", ForEach, 1, PropertyFlag.Configurable), PropertyFlags),
                ["includes"] = new(new ClrFunctionInstance(Engine, "includes", Includes, 1, PropertyFlag.Configurable), PropertyFlags),
                ["indexOf"] = new(new ClrFunctionInstance(Engine, "indexOf", IndexOf, 1, PropertyFlag.Configurable), PropertyFlags),
                ["join"] = new(new ClrFunctionInstance(Engine, "join", Join, 1, PropertyFlag.Configurable), PropertyFlags),
                ["keys"] = new(new ClrFunctionInstance(Engine, "keys", Keys, 0, PropertyFlag.Configurable), PropertyFlags),
                ["lastIndexOf"] = new(new ClrFunctionInstance(Engine, "lastIndexOf", LastIndexOf, 1, PropertyFlag.Configurable), PropertyFlags),
                ["length"] = new GetSetPropertyDescriptor(new ClrFunctionInstance(Engine, "get length", GetLength, 0, LengthFlags), Undefined, PropertyFlag.Configurable),
                ["map"] = new(new ClrFunctionInstance(Engine, "map", Map, 1, PropertyFlag.Configurable), PropertyFlags),
                ["reduce"] = new(new ClrFunctionInstance(Engine, "reduce", Reduce, 1, PropertyFlag.Configurable), PropertyFlags),
                ["reduceRight"] = new(new ClrFunctionInstance(Engine, "reduceRight", ReduceRight, 1, PropertyFlag.Configurable), PropertyFlags),
                ["reverse"] = new(new ClrFunctionInstance(Engine, "reverse", Reverse, 0, PropertyFlag.Configurable), PropertyFlags),
                ["set"] = new(new ClrFunctionInstance(Engine, "set", Set, 1, PropertyFlag.Configurable), PropertyFlags),
                ["slice"] = new(new ClrFunctionInstance(Engine, "slice", Slice, 2, PropertyFlag.Configurable), PropertyFlags),
                ["some"] = new(new ClrFunctionInstance(Engine, "some", Some, 1, PropertyFlag.Configurable), PropertyFlags),
                ["sort"] = new(new ClrFunctionInstance(Engine, "sort", Sort, 1, PropertyFlag.Configurable), PropertyFlags),
                ["subarray"] = new(new ClrFunctionInstance(Engine, "subarray", Subarray, 2, PropertyFlag.Configurable), PropertyFlags),
                ["toLocaleString"] = new(new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString, 0, PropertyFlag.Configurable), PropertyFlags),
                ["toReversed"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toReversed", ToReversed, 0, PropertyFlag.Configurable), PropertyFlags),
                ["toSorted"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toSorted", ToSorted, 1, PropertyFlag.Configurable), PropertyFlags),
                ["toString"] = new(new ClrFunctionInstance(Engine, "toLocaleString", _realm.Intrinsics.Array.PrototypeObject.ToString, 0, PropertyFlag.Configurable), PropertyFlags),
                ["values"] = new(new ClrFunctionInstance(Engine, "values", Values, 0, PropertyFlag.Configurable), PropertyFlags),
                ["with"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "with", With, 2, PropertyFlag.Configurable), PropertyFlags),
            };
            SetProperties(properties);

            _originalIteratorFunction = new ClrFunctionInstance(Engine, "iterator", Values, 1);
            var symbols = new SymbolDictionary(2)
            {
                [GlobalSymbolRegistry.Iterator] = new(_originalIteratorFunction, PropertyFlags),
                [GlobalSymbolRegistry.ToStringTag] = new GetSetPropertyDescriptor(new ClrFunctionInstance(Engine, "get [Symbol.toStringTag]", ToStringTag, 0, PropertyFlag.Configurable), Undefined, PropertyFlag.Configurable)
            };
            SetSymbols(symbols);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-get-%typedarray%.prototype.buffer
        /// </summary>
        private JsValue Buffer(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject as JsTypedArray;
            if (o is null)
            {
                ExceptionHelper.ThrowTypeError(_realm);
            }

            return o._viewedArrayBuffer;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-get-%typedarray%.prototype.bytelength
        /// </summary>
        private JsValue ByteLength(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject as JsTypedArray;
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
        private JsValue ByteOffset(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject as JsTypedArray;
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
        private JsValue GetLength(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject as JsTypedArray;
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
        private JsValue CopyWithin(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);

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
        private JsValue Entries(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);
            return _realm.Intrinsics.ArrayIteratorPrototype.Construct(o, ArrayIteratorType.KeyAndValue);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.every
        /// </summary>
        private JsValue Every(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);
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
        private JsValue Fill(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);

            var jsValue = arguments.At(0);
            var start = arguments.At(1);
            var end = arguments.At(2);

            JsValue value;
            if (o._contentType == TypedArrayContentType.BigInt)
            {
                value = JsBigInt.Create(jsValue.ToBigInteger(_engine));
            }
            else
            {
                value = JsNumber.Create(jsValue);
            }

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

            return thisObject;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.filter
        /// </summary>
        private JsValue Filter(JsValue thisObject, JsValue[] arguments)
        {
            var callbackfn = GetCallable(arguments.At(0));
            var thisArg = arguments.At(1);

            var o = thisObject.ValidateTypedArray(_realm);
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
        private JsValue Find(JsValue thisObject, JsValue[] arguments)
        {
            return DoFind(thisObject, arguments).Value;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.findindex
        /// </summary>
        private JsValue FindIndex(JsValue thisObject, JsValue[] arguments)
        {
            return DoFind(thisObject, arguments).Key;
        }

        private JsValue FindLast(JsValue thisObject, JsValue[] arguments)
        {
            return DoFind(thisObject, arguments, fromEnd: true).Value;
        }

        private JsValue FindLastIndex(JsValue thisObject, JsValue[] arguments)
        {
            return DoFind(thisObject, arguments, fromEnd: true).Key;
        }

        private KeyValuePair<JsValue, JsValue> DoFind(JsValue thisObject, JsValue[] arguments, bool fromEnd = false)
        {
            var o = thisObject.ValidateTypedArray(_realm);
            var len = (int) o.Length;

            var predicate = GetCallable(arguments.At(0));
            var thisArg = arguments.At(1);

            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o;
            if (!fromEnd)
            {
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
            }
            else
            {
                for (var k = len - 1; k >= 0; k--)
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
            }

            return new KeyValuePair<JsValue, JsValue>(JsNumber.IntegerNegativeOne, Undefined);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.foreach
        /// </summary>
        private JsValue ForEach(JsValue thisObject, JsValue[] arguments)
        {
            var callbackfn = GetCallable(arguments.At(0));
            var thisArg = arguments.At(1);

            var o = thisObject.ValidateTypedArray(_realm);
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
        private JsValue Includes(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);
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
                if (SameValueZeroComparer.Equals(value, searchElement))
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
        private JsValue IndexOf(JsValue thisObject, JsValue[] arguments)
        {
            var searchElement = arguments.At(0);
            var fromIndex = arguments.At(1);

            var o = thisObject.ValidateTypedArray(_realm);
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
                    if (elementK == searchElement)
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
        private JsValue Join(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);

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
        private JsValue Keys(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);
            return _realm.Intrinsics.ArrayIteratorPrototype.Construct(o, ArrayIteratorType.Key);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.lastindexof
        /// </summary>
        private JsValue LastIndexOf(JsValue thisObject, JsValue[] arguments)
        {
            var searchElement = arguments.At(0);

            var o = thisObject.ValidateTypedArray(_realm);
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
                    if (elementK == searchElement)
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
        private ObjectInstance Map(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);
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
        private JsValue Reduce(JsValue thisObject, JsValue[] arguments)
        {
            var callbackfn = GetCallable(arguments.At(0));
            var initialValue = arguments.At(1);

            var o = thisObject.ValidateTypedArray(_realm);
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
        private JsValue ReduceRight(JsValue thisObject, JsValue[] arguments)
        {
            var callbackfn = GetCallable(arguments.At(0));
            var initialValue = arguments.At(1);

            var o = thisObject.ValidateTypedArray(_realm);
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
        private ObjectInstance Reverse(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);
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
        private JsValue Set(JsValue thisObject, JsValue[] arguments)
        {
            var target = thisObject as JsTypedArray;
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

            if (source is JsTypedArray typedArrayInstance)
            {
                SetTypedArrayFromTypedArray(target, targetOffset, typedArrayInstance);
            }
            else
            {
                SetTypedArrayFromArrayLike(target, (int) targetOffset, source);
            }

            return Undefined;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-settypedarrayfromtypedarray
        /// </summary>
        private void SetTypedArrayFromTypedArray(JsTypedArray target, double targetOffset, JsTypedArray source)
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
                srcBuffer = srcBuffer.CloneArrayBuffer(_realm.Intrinsics.ArrayBuffer, srcByteOffset, srcByteLength);
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
        private void SetTypedArrayFromArrayLike(JsTypedArray target, int targetOffset, JsValue source)
        {
            var targetBuffer = target._viewedArrayBuffer;
            targetBuffer.AssertNotDetached();

            var targetLength = target._arrayLength;
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

            var k = 0;
            while (k < srcLength)
            {
                var jsValue = src.Get((ulong) k);
                target.IntegerIndexedElementSet(targetOffset + k, jsValue);
                k++;
            }
        }

        /// <summary>
        /// https://tc39.es/proposal-relative-indexing-method/#sec-%typedarray.prototype%-additions
        /// </summary>
        private JsValue At(JsValue thisObject, JsValue[] arguments)
        {
            var start = arguments.At(0);

            var o = thisObject.ValidateTypedArray(_realm);
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

            if (k < 0 || k >= len)
            {
                return Undefined;
            }

            return o.Get(k);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.slice
        /// </summary>
        private JsValue Slice(JsValue thisObject, JsValue[] arguments)
        {
            var start = arguments.At(0);
            var end = arguments.At(1);

            var o = thisObject.ValidateTypedArray(_realm);
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
                        var value = srcBuffer.GetValueFromBuffer(srcByteIndex, TypedArrayElementType.Uint8, true, ArrayBufferOrder.Unordered);
                        targetBuffer.SetValueInBuffer(targetByteIndex, TypedArrayElementType.Uint8, value, true, ArrayBufferOrder.Unordered);
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
        private JsValue Some(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);
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
        private JsValue Sort(JsValue thisObject, JsValue[] arguments)
        {
            /*
             * %TypedArray%.prototype.sort is a distinct function that, except as described below,
             * implements the same requirements as those of Array.prototype.sort as defined in 23.1.3.27.
             * The implementation of the %TypedArray%.prototype.sort specification may be optimized with the knowledge that the this value is
             * an object that has a fixed length and whose integer-indexed properties are not sparse.
             */

            var obj = thisObject.ValidateTypedArray(_realm);
            var buffer = obj._viewedArrayBuffer;
            var len = obj.Length;

            var compareFn = GetCompareFunction(arguments.At(0));

            if (len <= 1)
            {
                return obj;
            }

            var array = SortArray(buffer, compareFn, obj);

            for (var i = 0; i < (uint) array.Length; ++i)
            {
                obj[i] = array[i];
            }

            return obj;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-%typedarray%.prototype.subarray
        /// </summary>
        private JsValue Subarray(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject as JsTypedArray;
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
        private JsValue ToLocaleString(JsValue thisObject, JsValue[] arguments)
        {
            /*
             * %TypedArray%.prototype.toLocaleString is a distinct function that implements the same algorithm as Array.prototype.toLocaleString
             * as defined in 23.1.3.29 except that the this value's [[ArrayLength]] internal slot is accessed in place of performing
             * a [[Get]] of "length". The implementation of the algorithm may be optimized with the knowledge that the this value is an object
             * that has a fixed length and whose integer-indexed properties are not sparse. However, such optimization must not introduce
             * any observable changes in the specified behaviour of the algorithm.
             */

            var array = thisObject.ValidateTypedArray(_realm);
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
                var func = elementObj.Get("toLocaleString") as ICallable;
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
                var func = elementObj.Get("toLocaleString") as ICallable;
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
        private JsValue Values(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);
            return _realm.Intrinsics.ArrayIteratorPrototype.Construct(o, ArrayIteratorType.Value);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-get-%typedarray%.prototype-@@tostringtag
        /// </summary>
        private static JsValue ToStringTag(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject is not JsTypedArray o)
            {
                return Undefined;
            }

            return o._arrayElementType.GetTypedArrayName();
        }

        private JsValue ToReversed(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);
            var length = o._arrayLength;

            var a = TypedArrayCreateSameType(o, new [] { JsNumber.Create(length) });
            uint k = 0;
            while (k < length)
            {
                var from = length - k - 1;
                a[k++] = o.Get(from);
            }

            return a;
        }

        private JsValue ToSorted(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);
            var compareFn = GetCompareFunction(arguments.At(0));

            var buffer = o._viewedArrayBuffer;
            var length = o.Length;

            var a = TypedArrayCreateSameType(o, new [] { JsNumber.Create(length) });

            var array = SortArray(buffer, compareFn, o);
            for (var i = 0; (uint) i < (uint) array.Length; ++i)
            {
                a[i] = array[i];
            }

            return a;
        }

        private ObjectInstance With(JsValue thisObject, JsValue[] arguments)
        {
            var o = thisObject.ValidateTypedArray(_realm);
            var value = arguments.At(1);

            var length = o._arrayLength;
            var relativeIndex = TypeConverter.ToIntegerOrInfinity(arguments.At(0));

            long actualIndex;
            if (relativeIndex >= 0)
            {
                actualIndex = (long) relativeIndex;
            }
            else
            {
                actualIndex = (long) (length + relativeIndex);
            }

            value = o._contentType == TypedArrayContentType.BigInt
                ? TypeConverter.ToJsBigInt(value)
                : TypeConverter.ToJsNumber(value);

            if (!o.IsValidIntegerIndex(actualIndex))
            {
                ExceptionHelper.ThrowRangeError(_realm, "Invalid start index");
            }

            var a = TypedArrayCreateSameType(o, new [] { JsNumber.Create(length) });

            var k = 0;
            while (k < length)
            {
                a[k] = k == (int) actualIndex ? value : o.Get(k);
                k++;
            }

            return a;
        }

        private JsTypedArray TypedArrayCreateSameType(JsTypedArray exemplar, JsValue[] argumentList)
        {
            var constructor = exemplar._arrayElementType.GetConstructor(_realm.Intrinsics);
            var result = IntrinsicTypedArrayConstructor.TypedArrayCreate(_realm, constructor, argumentList);
            return result;
        }

        private ICallable? GetCompareFunction(JsValue compareArg)
        {
            ICallable? compareFn = null;
            if (!compareArg.IsUndefined())
            {
                if (compareArg is not ICallable callable)
                {
                    ExceptionHelper.ThrowTypeError(_realm, "The comparison function must be either a function or undefined");
                    return null;
                }
                compareFn = callable;
            }

            return compareFn;
        }

        private static JsValue[] SortArray(JsArrayBuffer buffer, ICallable? compareFn, JsTypedArray obj)
        {
            var comparer = TypedArrayComparer.WithFunction(buffer, compareFn);
            var operations = ArrayOperations.For(obj);
            try
            {
                return operations.OrderBy(x => x, comparer).ToArray();
            }
            catch (InvalidOperationException e)
            {
                throw e.InnerException ?? e;
            }
        }

        private sealed class TypedArrayComparer : IComparer<JsValue>
        {
            public static TypedArrayComparer WithFunction(JsArrayBuffer buffer, ICallable? compare)
            {
                return new TypedArrayComparer(buffer, compare);
            }

            private readonly JsArrayBuffer _buffer;
            private readonly ICallable? _compare;
            private readonly JsValue[] _comparableArray = new JsValue[2];

            private TypedArrayComparer(JsArrayBuffer buffer, ICallable? compare)
            {
                _buffer = buffer;
                _compare = compare;
            }

            public int Compare(JsValue? x, JsValue? y)
            {
                if (x is null && y is null)
                {
                    return 0;
                }

                if (x is not null && y is null)
                {
                    return 1;
                }

                if (x is null)
                {
                    return -1;
                }

                if (y is null)
                {
                    return 1;
                }

                if (_compare is not null)
                {
                    _comparableArray[0] = x;
                    _comparableArray[1] = y;

                    var v = TypeConverter.ToNumber(_compare.Call(Undefined, _comparableArray));

                    if (double.IsNaN(v))
                    {
                        return 0;
                    }

                    return (int) v;
                }

                if (x.Type == Types.BigInt || y.Type == Types.BigInt)
                {
                    var xBigInt = TypeConverter.ToBigInt(x);
                    var yBigInt = TypeConverter.ToBigInt(y);
                    return xBigInt.CompareTo(yBigInt);
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
