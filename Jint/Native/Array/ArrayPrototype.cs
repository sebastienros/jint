using Jint.Collections;
using Jint.Native.Iterator;
using Jint.Native.Map;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.Array
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-properties-of-the-array-prototype-object
    /// </summary>
    public sealed class ArrayPrototype : ArrayInstance
    {
        private readonly Realm _realm;
        private readonly ArrayConstructor _constructor;
        internal ClrFunctionInstance? _originalIteratorFunction;

        internal ArrayPrototype(
            Engine engine,
            Realm realm,
            ArrayConstructor arrayConstructor,
            ObjectPrototype objectPrototype) : base(engine)
        {
            _prototype = objectPrototype;
            _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Writable);
            _realm = realm;
            _constructor = arrayConstructor;
        }

        protected override void Initialize()
        {
            const PropertyFlag propertyFlags = PropertyFlag.Writable | PropertyFlag.Configurable;
            var properties = new PropertyDictionary(36, checkExistingKeys: false)
            {
                ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
                ["toString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toString", ToString, 0, PropertyFlag.Configurable), propertyFlags),
                ["toLocaleString"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "toLocaleString", ToLocaleString, 0, PropertyFlag.Configurable), propertyFlags),
                ["concat"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "concat", Concat, 1, PropertyFlag.Configurable), propertyFlags),
                ["copyWithin"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "copyWithin", CopyWithin, 2, PropertyFlag.Configurable), propertyFlags),
                ["entries"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "entries", Entries, 0, PropertyFlag.Configurable), propertyFlags),
                ["fill"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "fill", Fill, 1, PropertyFlag.Configurable), propertyFlags),
                ["join"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "join", Join, 1, PropertyFlag.Configurable), propertyFlags),
                ["pop"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "pop", Pop, 0, PropertyFlag.Configurable), propertyFlags),
                ["push"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "push", Push, 1, PropertyFlag.Configurable), propertyFlags),
                ["reverse"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "reverse", Reverse, 0, PropertyFlag.Configurable), propertyFlags),
                ["shift"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "shift", Shift, 0, PropertyFlag.Configurable), propertyFlags),
                ["slice"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "slice", Slice, 2, PropertyFlag.Configurable), propertyFlags),
                ["sort"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "sort", Sort, 1, PropertyFlag.Configurable), propertyFlags),
                ["splice"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "splice", Splice, 2, PropertyFlag.Configurable), propertyFlags),
                ["unshift"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "unshift", Unshift, 1, PropertyFlag.Configurable), propertyFlags),
                ["includes"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "includes", Includes, 1, PropertyFlag.Configurable), propertyFlags),
                ["indexOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "indexOf", IndexOf, 1, PropertyFlag.Configurable), propertyFlags),
                ["lastIndexOf"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "lastIndexOf", LastIndexOf, 1, PropertyFlag.Configurable), propertyFlags),
                ["every"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "every", Every, 1, PropertyFlag.Configurable), propertyFlags),
                ["some"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "some", Some, 1, PropertyFlag.Configurable), propertyFlags),
                ["forEach"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "forEach", ForEach, 1, PropertyFlag.Configurable), propertyFlags),
                ["map"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "map", Map, 1, PropertyFlag.Configurable), propertyFlags),
                ["filter"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "filter", Filter, 1, PropertyFlag.Configurable), propertyFlags),
                ["reduce"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "reduce", Reduce, 1, PropertyFlag.Configurable), propertyFlags),
                ["reduceRight"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "reduceRight", ReduceRight, 1, PropertyFlag.Configurable), propertyFlags),
                ["find"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "find", Find, 1, PropertyFlag.Configurable), propertyFlags),
                ["findIndex"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "findIndex", FindIndex, 1, PropertyFlag.Configurable), propertyFlags),
                ["findLast"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "findLast", FindLast, 1, PropertyFlag.Configurable), propertyFlags),
                ["findLastIndex"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "findLastIndex", FindLastIndex, 1, PropertyFlag.Configurable), propertyFlags),
                ["keys"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "keys", Keys, 0, PropertyFlag.Configurable), propertyFlags),
                ["values"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "values", Values, 0, PropertyFlag.Configurable), propertyFlags),
                ["flat"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "flat", Flat, 0, PropertyFlag.Configurable), propertyFlags),
                ["flatMap"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "flatMap", FlatMap, 1, PropertyFlag.Configurable), propertyFlags),
                ["at"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "at", At, 1, PropertyFlag.Configurable), propertyFlags),
                ["group"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "group", Group, 1, PropertyFlag.Configurable), propertyFlags),
                ["groupToMap"] = new PropertyDescriptor(new ClrFunctionInstance(Engine, "groupToMap", GroupToMap, 1, PropertyFlag.Configurable), propertyFlags),
            };
            SetProperties(properties);

            _originalIteratorFunction = new ClrFunctionInstance(Engine, "iterator", Values, 1);
            var symbols = new SymbolDictionary(2)
            {
                [GlobalSymbolRegistry.Iterator] = new PropertyDescriptor(_originalIteratorFunction, propertyFlags),
                [GlobalSymbolRegistry.Unscopables] = new LazyPropertyDescriptor(_engine, static state =>
                {
                    var unscopables = new ObjectInstance((Engine) state!)
                    {
                        _prototype = null
                    };

                    unscopables.SetDataProperty("at", JsBoolean.True);
                    unscopables.SetDataProperty("copyWithin", JsBoolean.True);
                    unscopables.SetDataProperty("entries", JsBoolean.True);
                    unscopables.SetDataProperty("fill", JsBoolean.True);
                    unscopables.SetDataProperty("find", JsBoolean.True);
                    unscopables.SetDataProperty("findIndex", JsBoolean.True);
                    unscopables.SetDataProperty("findLast", JsBoolean.True);
                    unscopables.SetDataProperty("findLastIndex", JsBoolean.True);
                    unscopables.SetDataProperty("flat", JsBoolean.True);
                    unscopables.SetDataProperty("flatMap", JsBoolean.True);
                    unscopables.SetDataProperty("groupBy", JsBoolean.True);
                    unscopables.SetDataProperty("groupByToMap", JsBoolean.True);
                    unscopables.SetDataProperty("includes", JsBoolean.True);
                    unscopables.SetDataProperty("keys", JsBoolean.True);
                    unscopables.SetDataProperty("values", JsBoolean.True);
                    unscopables.SetDataProperty("group", JsBoolean.True);
                    unscopables.SetDataProperty("groupToMap", JsBoolean.True);

                    return unscopables;
                }, PropertyFlag.Configurable)
            };
            SetSymbols(symbols);
        }

        private ObjectInstance Keys(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj is ObjectInstance oi && oi.IsArrayLike)
            {
                return _realm.Intrinsics.ArrayIteratorPrototype.Construct(oi, ArrayIteratorType.Key);
            }

            ExceptionHelper.ThrowTypeError(_realm, "cannot construct iterator");
            return null;
        }

        internal ObjectInstance Values(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj is ObjectInstance oi && oi.IsArrayLike)
            {
                return _realm.Intrinsics.ArrayIteratorPrototype.Construct(oi, ArrayIteratorType.Value);
            }

            ExceptionHelper.ThrowTypeError(_realm, "cannot construct iterator");
            return null;
        }

        private ObjectInstance Entries(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj is ObjectInstance oi && oi.IsArrayLike)
            {
                return _realm.Intrinsics.ArrayIteratorPrototype.Construct(oi, ArrayIteratorType.KeyAndValue);
            }

            ExceptionHelper.ThrowTypeError(_realm, "cannot construct iterator");
            return null;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.fill
        /// </summary>
        private JsValue Fill(JsValue thisObj, JsValue[] arguments)
        {
            var value = arguments.At(0);
            var start = arguments.At(1);
            var end = arguments.At(2);

            var o = TypeConverter.ToObject(_realm, thisObj);

            var operations = ArrayOperations.For(o);
            var length = operations.GetLongLength();

            var relativeStart = TypeConverter.ToIntegerOrInfinity(start);

            ulong k;
            if (double.IsNegativeInfinity(relativeStart))
            {
                k = 0;
            }
            else if (relativeStart < 0)
            {
                k = (ulong) System.Math.Max(length + relativeStart, 0);
            }
            else
            {
                k = (ulong) System.Math.Min(relativeStart, length);
            }

            var relativeEnd = end.IsUndefined() ? length : TypeConverter.ToIntegerOrInfinity(end);
            ulong final;
            if (double.IsNegativeInfinity(relativeEnd))
            {
                final = 0;
            }
            else if (relativeEnd < 0)
            {
                final = (ulong) System.Math.Max(length + relativeEnd, 0);
            }
            else
            {
                final = (ulong) System.Math.Min(relativeEnd, length);
            }

            for (var i = k; i < final; ++i)
            {
                operations.Set(i, value, throwOnError: false);
            }

            return o;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.copywithin
        /// </summary>
        private JsValue CopyWithin(JsValue thisObj, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, thisObj);

            JsValue target = arguments.At(0);
            JsValue start = arguments.At(1);
            JsValue end = arguments.At(2);

            var operations = ArrayOperations.For(o);
            var len = operations.GetLongLength();

            var relativeTarget = TypeConverter.ToIntegerOrInfinity(target);

            var to = relativeTarget < 0 ?
                System.Math.Max(len + relativeTarget, 0) :
                System.Math.Min(relativeTarget, len);

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

            var relativeEnd = end.IsUndefined() ? len : TypeConverter.ToIntegerOrInfinity(end);

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

            var count = (long) System.Math.Min(final - from, len - to);

            long direction = 1;

            if (from < to && to < from + count)
            {
                direction = -1;
                from += count - 1;
                to += count - 1;
            }

            while (count > 0)
            {
                var fromPresent = operations.HasProperty((ulong) from);
                if (fromPresent)
                {
                    var fromValue = operations.Get((ulong) from);
                    operations.Set((ulong) to, fromValue, updateLength: true, throwOnError: true);
                }
                else
                {
                    operations.DeletePropertyOrThrow((ulong) to);
                }
                from += direction;
                to += direction;
                count--;
            }

            return o;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.lastindexof
        /// </summary>
        private JsValue LastIndexOf(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(_realm, thisObj);
            var len = o.GetLongLength();
            if (len == 0)
            {
                return JsNumber.IntegerNegativeOne;
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

            if (k < 0 || k > ArrayOperations.MaxArrayLikeLength)
            {
                return JsNumber.IntegerNegativeOne;
            }

            var searchElement = arguments.At(0);
            var i = (ulong) k;
            for (; ; i--)
            {
                var kPresent = o.HasProperty(i);
                if (kPresent)
                {
                    var elementK = o.Get(i);
                    if (elementK == searchElement)
                    {
                        return i;
                    }
                }

                if (i == 0)
                {
                    break;
                }
            }

            return JsNumber.IntegerNegativeOne;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.reduce
        /// </summary>
        private JsValue Reduce(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var initialValue = arguments.At(1);

            var o = ArrayOperations.For(_realm, thisObj);
            var len = o.GetLength();

            var callable = GetCallable(callbackfn);

            if (len == 0 && arguments.Length < 2)
            {
                ExceptionHelper.ThrowTypeError(_realm);
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
                    ExceptionHelper.ThrowTypeError(_realm);
                }
            }

            var args = new JsValue[4];
            args[3] = o.Target;
            while (k < len)
            {
                var i = (uint) k;
                if (o.TryGetValue(i, out var kvalue))
                {
                    args[0] = accumulator;
                    args[1] = kvalue;
                    args[2] = i;
                    accumulator = callable.Call(Undefined, args);
                }

                k++;
            }

            return accumulator;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.filter
        /// </summary>
        private JsValue Filter(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var o = ArrayOperations.For(_realm, thisObj);
            var len = o.GetLength();

            var callable = GetCallable(callbackfn);

            var a = _realm.Intrinsics.Array.ArraySpeciesCreate(TypeConverter.ToObject(_realm, thisObj), 0);
            var operations = ArrayOperations.For(a);

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
                        operations.CreateDataPropertyOrThrow(to, kvalue);
                        to++;
                    }
                }
            }

            operations.SetLength(to);
            _engine._jsValueArrayPool.ReturnArray(args);

            return a;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.map
        /// </summary>
        private JsValue Map(JsValue thisObj, JsValue[] arguments)
        {
            if (thisObj is ArrayInstance { CanUseFastAccess: true } arrayInstance
                && !arrayInstance.HasOwnProperty(CommonProperties.Constructor))
            {
                return arrayInstance.Map(arguments);
            }

            var o = ArrayOperations.For(_realm, thisObj);
            var len = o.GetLongLength();

            if (len > ArrayOperations.MaxArrayLength)
            {
                ExceptionHelper.ThrowRangeError(_realm, "Invalid array length");;
            }

            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);
            var callable = GetCallable(callbackfn);

            var a = ArrayOperations.For(_realm.Intrinsics.Array.ArraySpeciesCreate(TypeConverter.ToObject(_realm, thisObj), (uint) len));
            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o.Target;
            for (uint k = 0; k < len; k++)
            {
                if (o.TryGetValue(k, out var kvalue))
                {
                    args[0] = kvalue;
                    args[1] = k;
                    var mappedValue = callable.Call(thisArg, args);
                    a.CreateDataPropertyOrThrow(k, mappedValue);
                }
            }
            _engine._jsValueArrayPool.ReturnArray(args);
            return a.Target;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.flat
        /// </summary>
        private JsValue Flat(JsValue thisObj, JsValue[] arguments)
        {
            var O = TypeConverter.ToObject(_realm, thisObj);
            var operations = ArrayOperations.For(O);
            var sourceLen = operations.GetLength();
            double depthNum = 1;
            var depth = arguments.At(0);
            if (!depth.IsUndefined())
            {
                depthNum = TypeConverter.ToIntegerOrInfinity(depth);
            }

            if (depthNum < 0)
            {
                depthNum = 0;
            }

            var A = _realm.Intrinsics.Array.ArraySpeciesCreate(O, 0);
            FlattenIntoArray(A, O, sourceLen, 0, depthNum);
            return A;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.flatmap
        /// </summary>
        private JsValue FlatMap(JsValue thisObj, JsValue[] arguments)
        {
            var O = TypeConverter.ToObject(_realm, thisObj);
            var mapperFunction = arguments.At(0);
            var thisArg = arguments.At(1);

            var sourceLen = O.Length;

            if (!mapperFunction.IsCallable)
            {
                ExceptionHelper.ThrowTypeError(_realm, "flatMap mapper function is not callable");
            }

            var A = _realm.Intrinsics.Array.ArraySpeciesCreate(O, 0);
            FlattenIntoArray(A, O, sourceLen, 0, 1, (ICallable) mapperFunction, thisArg);
            return A;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-flattenintoarray
        /// </summary>
        private long FlattenIntoArray(
            ObjectInstance target,
            ObjectInstance source,
            uint sourceLen,
            long start,
            double depth,
            ICallable? mapperFunction = null,
            JsValue? thisArg = null)
        {
            var targetIndex = start;
            var sourceIndex = 0;

            var callArguments = System.Array.Empty<JsValue>();
            if (mapperFunction is not null)
            {
                callArguments = _engine._jsValueArrayPool.RentArray(3);
                callArguments[2] = source;
            }

            while (sourceIndex < sourceLen)
            {
                var P = TypeConverter.ToString(sourceIndex);
                var exists = source.HasProperty(P);
                if (exists)
                {
                    var element = source.Get(P);
                    if (mapperFunction is not null)
                    {
                        callArguments[0] = element;
                        callArguments[1] = JsNumber.Create(sourceIndex);
                        element = mapperFunction.Call(thisArg ?? Undefined, callArguments);
                    }

                    var shouldFlatten = false;
                    if (depth > 0)
                    {
                        shouldFlatten = element.IsArray();
                    }

                    if (shouldFlatten)
                    {
                        var newDepth = double.IsPositiveInfinity(depth)
                            ? depth
                            : depth - 1;

                        var objectInstance = (ObjectInstance) element;
                        var elementLen = objectInstance.Length;
                        targetIndex = FlattenIntoArray(target, objectInstance, elementLen, targetIndex, newDepth);
                    }
                    else
                    {
                        if (targetIndex >= NumberConstructor.MaxSafeInteger)
                        {
                            ExceptionHelper.ThrowTypeError(_realm);
                        }

                        target.CreateDataPropertyOrThrow(targetIndex, element);
                        targetIndex += 1;
                    }
                }

                sourceIndex++;
            }

            if (mapperFunction is not null)
            {
                _engine._jsValueArrayPool.ReturnArray(callArguments);
            }

            return targetIndex;
        }

        private JsValue ForEach(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);

            var o = ArrayOperations.For(_realm, thisObj);
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

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.includes
        /// </summary>
        private JsValue Includes(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(_realm, thisObj);
            var len = (long) o.GetLongLength();

            if (len == 0)
            {
                return JsBoolean.False;
            }

            var searchElement = arguments.At(0);
            var fromIndex = arguments.At(1);

            long k = 0;
            var n = TypeConverter.ToIntegerOrInfinity(fromIndex);
            if (double.IsPositiveInfinity(n))
            {
                return JsBoolean.False;
            }
            else if (double.IsNegativeInfinity(n))
            {
                n = 0;
            }
            else if (n >= 0)
            {
                k = (long) n;
            }
            else
            {
                k = len + (long) n;
                if (k < 0)
                {
                    k = 0;
                }
            }

            while (k < len)
            {
                var value = o.Get((ulong) k);
                if (SameValueZeroComparer.Equals(value, searchElement))
                {
                    return true;
                }
                k++;
            }
            return false;
        }

        private JsValue Some(JsValue thisObj, JsValue[] arguments)
        {
            var target = TypeConverter.ToObject(_realm, thisObj);
            return target.FindWithCallback(arguments, out _, out _, false);
        }

        private JsValue Every(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(_realm, thisObj);
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

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.indexof
        /// </summary>
        private JsValue IndexOf(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(_realm, thisObj);
            var len = o.GetLongLength();
            if (len == 0)
            {
                return -1;
            }

            var startIndex = arguments.Length > 1
                ? TypeConverter.ToIntegerOrInfinity(arguments.At(1))
                : 0;

            if (startIndex > ArrayOperations.MaxArrayLikeLength)
            {
                return JsNumber.IntegerNegativeOne;
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
                var kPresent = o.HasProperty(k);
                if (kPresent)
                {
                    var elementK = o.Get(k);
                    if (elementK == searchElement)
                    {
                        return k;
                    }
                }
            }

            return -1;
        }

        private JsValue Find(JsValue thisObj, JsValue[] arguments)
        {
            var target = TypeConverter.ToObject(_realm, thisObj);
            target.FindWithCallback(arguments, out _, out var value, visitUnassigned: true);
            return value;
        }

        private JsValue FindIndex(JsValue thisObj, JsValue[] arguments)
        {
            var target = TypeConverter.ToObject(_realm, thisObj);
            if (target.FindWithCallback(arguments, out var index, out _, visitUnassigned: true))
            {
                return index;
            }
            return -1;
        }

        private JsValue FindLast(JsValue thisObj, JsValue[] arguments)
        {
            var target = TypeConverter.ToObject(_realm, thisObj);
            target.FindWithCallback(arguments, out _, out var value, visitUnassigned: true, fromEnd: true);
            return value;
        }

        private JsValue FindLastIndex(JsValue thisObj, JsValue[] arguments)
        {
            var target = TypeConverter.ToObject(_realm, thisObj);
            if (target.FindWithCallback(arguments, out var index, out _, visitUnassigned: true, fromEnd: true))
            {
                return index;
            }
            return -1;
        }

        /// <summary>
        /// https://tc39.es/proposal-relative-indexing-method/#sec-array-prototype-additions
        /// </summary>
        private JsValue At(JsValue thisObj, JsValue[] arguments)
        {
            var target = TypeConverter.ToObject(_realm, thisObj);
            var len = target.Length;
            var relativeIndex = TypeConverter.ToInteger(arguments.At(0));

            ulong actualIndex;
            if (relativeIndex < 0)
            {
                actualIndex = (ulong) (len + relativeIndex);
            }
            else
            {
                actualIndex = (ulong) relativeIndex;
            }

            if (actualIndex < 0 || actualIndex >= len)
            {
                return Undefined;
            }

            return target.Get(actualIndex);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.splice
        /// </summary>
        private JsValue Splice(JsValue thisObj, JsValue[] arguments)
        {
            var start = arguments.At(0);
            var deleteCount = arguments.At(1);

            var obj = TypeConverter.ToObject(_realm, thisObj);
            var o = ArrayOperations.For(_realm, obj);
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

            var items = System.Array.Empty<JsValue>();
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

                items = System.Array.Empty<JsValue>();
                if (arguments.Length > 2)
                {
                    items = new JsValue[arguments.Length - 2];
                    System.Array.Copy(arguments, 2, items, 0, items.Length);
                }
            }

            if (len + insertCount - actualDeleteCount > ArrayOperations.MaxArrayLikeLength)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Invalid array length");
            }

            var instance = _realm.Intrinsics.Array.ArraySpeciesCreate(obj, actualDeleteCount);
            var a = ArrayOperations.For(instance);
            for (uint k = 0; k < actualDeleteCount; k++)
            {
                var index = actualStart + k;
                if (o.HasProperty(index))
                {
                    var fromValue = o.Get(index);
                    a.CreateDataPropertyOrThrow(k, fromValue);
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
                    if (o.HasProperty(from))
                    {
                        var fromValue = o.Get(from);
                        o.Set(to, fromValue, throwOnError: false);
                    }
                    else
                    {
                        o.DeletePropertyOrThrow(to);
                    }
                }

                for (var k = len; k > len - actualDeleteCount + (ulong) items.Length; k--)
                {
                    o.DeletePropertyOrThrow(k - 1);
                }
            }
            else if ((ulong) items.Length > actualDeleteCount)
            {
                for (var k = len - actualDeleteCount; k > actualStart; k--)
                {
                    var from = k + actualDeleteCount - 1;
                    var to =  k + (ulong) items.Length - 1;
                    if (o.HasProperty(from))
                    {
                        var fromValue = o.Get(from);
                        o.Set(to, fromValue, throwOnError: true);
                    }
                    else
                    {
                        o.DeletePropertyOrThrow(to);
                    }
                }
            }

            for (uint k = 0; k < items.Length; k++)
            {
                var e = items[k];
                o.Set(k + actualStart, e, throwOnError: true);
            }

            o.SetLength(length);
            return a.Target;
        }

        /// <summary>
        /// /https://tc39.es/ecma262/#sec-array.prototype.unshift
        /// </summary>
        private JsValue Unshift(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(_realm, thisObj);
            var len = o.GetLongLength();
            var argCount = (uint) arguments.Length;

            if (len + argCount > ArrayOperations.MaxArrayLikeLength)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Invalid array length");
            }

            o.EnsureCapacity(len + argCount);
            var minIndex = o.GetSmallestIndex(len);
            for (var k = len; k > minIndex; k--)
            {
                var from = k - 1;
                var to = k + argCount - 1;
                if (o.TryGetValue(from, out var fromValue))
                {
                    o.Set(to, fromValue, updateLength: false);
                }
                else
                {
                    o.DeletePropertyOrThrow(to);
                }
            }

            for (uint j = 0; j < argCount; j++)
            {
                o.Set(j, arguments[j], updateLength: false);
            }

            o.SetLength(len + argCount);
            return len + argCount;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.sort
        /// </summary>
        private JsValue Sort(JsValue thisObj, JsValue[] arguments)
        {
            var objectInstance = TypeConverter.ToObject(_realm, thisObj);
            var obj = ArrayOperations.For(objectInstance);

            var compareArg = arguments.At(0);
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

            var len = obj.GetLength();
            if (len <= 1)
            {
                return obj.Target;
            }

            var items = new List<JsValue>((int) System.Math.Min(10_000, obj.GetLength()));
            for (ulong k = 0; k < len; ++k)
            {
                if (obj.TryGetValue(k, out var kValue))
                {
                    items.Add(kValue);
                }
            }

            var itemCount = items.Count;

            // don't eat inner exceptions
            try
            {
                var array = items.OrderBy(x => x, ArrayComparer.WithFunction(_engine, compareFn!)).ToArray();

                uint j;
                for (j = 0; j < itemCount; ++j)
                {
                    var updateLength = j == itemCount - 1;
                    obj.Set(j, array[j], updateLength: updateLength, throwOnError: true);
                }
                for (; j < len; ++j)
                {
                    obj.DeletePropertyOrThrow(j);
                }
            }
            catch (InvalidOperationException e)
            {
                throw e.InnerException ?? e;
            }

            return obj.Target;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.slice
        /// </summary>
        private JsValue Slice(JsValue thisObj, JsValue[] arguments)
        {
            var start = arguments.At(0);
            var end = arguments.At(1);

            var o = ArrayOperations.For(_realm, thisObj);
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
                    final = (ulong) System.Math.Min(relativeEnd, len);
                }
            }

            if (k < final && final - k > ArrayOperations.MaxArrayLength)
            {
                ExceptionHelper.ThrowRangeError(_realm, "Invalid array length");;
            }

            var length = (uint) System.Math.Max(0, (long) final - (long) k);
            var a = _realm.Intrinsics.Array.ArraySpeciesCreate(TypeConverter.ToObject(_realm, thisObj), length);
            if (thisObj is ArrayInstance ai && a is ArrayInstance a2)
            {
                a2.CopyValues(ai, (uint) k, 0, length);
            }
            else
            {
                // slower path
                var operations = ArrayOperations.For(a);
                for (uint n = 0; k < final; k++, n++)
                {
                    if (o.TryGetValue(k, out var kValue))
                    {
                        operations.CreateDataPropertyOrThrow(n, kValue);
                    }
                }
            }
            a.DefineOwnProperty(CommonProperties.Length, new PropertyDescriptor(length, PropertyFlag.ConfigurableEnumerableWritable));

            return a;
        }

        private JsValue Shift(JsValue thisObj, JsValue[] arg2)
        {
            var o = ArrayOperations.For(_realm, thisObj);
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
                    o.Set(to, fromVal, throwOnError: false);
                }
                else
                {
                    o.DeletePropertyOrThrow(to);
                }
            }

            o.DeletePropertyOrThrow(len - 1);
            o.SetLength(len - 1);

            return first;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.reverse
        /// </summary>
        private JsValue Reverse(JsValue thisObj, JsValue[] arguments)
        {
            var o = ArrayOperations.For(_realm, thisObj);
            var len = o.GetLongLength();
            var middle = (ulong) System.Math.Floor(len / 2.0);
            uint lower = 0;
            while (lower != middle)
            {
                var upper = len - lower - 1;

                var lowerExists = o.HasProperty(lower);
                var lowerValue = lowerExists ? o.Get(lower) : null;

                var upperExists = o.HasProperty(upper);
                var upperValue = upperExists ? o.Get(upper) : null;

                if (lowerExists && upperExists)
                {
                    o.Set(lower, upperValue!, throwOnError: true);
                    o.Set(upper, lowerValue!, throwOnError: true);
                }

                if (!lowerExists && upperExists)
                {
                    o.Set(lower, upperValue!, throwOnError: true);
                    o.DeletePropertyOrThrow(upper);
                }

                if (lowerExists && !upperExists)
                {
                    o.DeletePropertyOrThrow(lower);
                    o.Set(upper, lowerValue!, throwOnError: true);
                }

                lower++;
            }

            return o.Target;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.join
        /// </summary>
        private JsValue Join(JsValue thisObj, JsValue[] arguments)
        {
            var separator = arguments.At(0);
            var o = ArrayOperations.For(_realm, thisObj);
            var len = o.GetLength();

            var sep = TypeConverter.ToString(separator.IsUndefined() ? JsString.CommaString : separator);

            // as per the spec, this has to be called after ToString(separator)
            if (len == 0)
            {
                return JsString.Empty;
            }

            static string StringFromJsValue(JsValue value)
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

            using var sb = StringBuilderPool.Rent();
            sb.Builder.Append(s);
            for (uint k = 1; k < len; k++)
            {
                sb.Builder.Append(sep);
                sb.Builder.Append(StringFromJsValue(o.Get(k)));
            }

            return sb.ToString();
        }

        private JsValue ToLocaleString(JsValue thisObj, JsValue[] arguments)
        {
            var array = ArrayOperations.For(_realm, thisObj);
            var len = array.GetLength();
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
                if (!array.TryGetValue(k, out var nextElement) || nextElement.IsNull())
                {
                    r = JsString.Empty;
                }
                else
                {
                    var elementObj = TypeConverter.ToObject(_realm, nextElement);
                    var func = elementObj.Get("toLocaleString", elementObj) as ICallable;
                    if (func is null)
                    {
                        ExceptionHelper.ThrowTypeError(_realm);
                    }
                    r = func.Call(elementObj, Arguments.Empty);
                }

                r = s + r;
            }

            return r;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.concat
        /// </summary>
        private JsValue Concat(JsValue thisObj, JsValue[] arguments)
        {
            var o = TypeConverter.ToObject(_realm, thisObj);
            var items = new List<JsValue>(arguments.Length + 1) {o};
            items.AddRange(arguments);

            uint n = 0;
            var a = _realm.Intrinsics.Array.ArraySpeciesCreate(TypeConverter.ToObject(_realm, thisObj), 0);
            var aOperations = ArrayOperations.For(a);
            for (var i = 0; i < items.Count; i++)
            {
                var e = items[i];
                if (e is ObjectInstance oi && oi.IsConcatSpreadable)
                {
                    if (e is ArrayInstance eArray && a is ArrayInstance a2)
                    {
                        a2.CopyValues(eArray, 0, n, eArray.GetLength());
                        n += eArray.GetLength();
                    }
                    else
                    {
                        var operations = ArrayOperations.For(oi);
                        var len = operations.GetLongLength();

                        if (n + len > ArrayOperations.MaxArrayLikeLength)
                        {
                            ExceptionHelper.ThrowTypeError(_realm, "Invalid array length");
                        }

                        for (uint k = 0; k < len; k++)
                        {
                            operations.TryGetValue(k, out var subElement);
                            aOperations.CreateDataPropertyOrThrow(n, subElement);
                            n++;
                        }
                    }
                }
                else
                {
                    aOperations.CreateDataPropertyOrThrow(n, e);
                    n++;
                }
            }

            // this is not in the specs, but is necessary in case the last element of the last
            // array doesn't exist, and thus the length would not be incremented
            a.DefineOwnProperty(CommonProperties.Length, new PropertyDescriptor(n, PropertyFlag.OnlyWritable));

            return a;
        }

        internal JsValue ToString(JsValue thisObj, JsValue[] arguments)
        {
            var array = TypeConverter.ToObject(_realm, thisObj);

            Func<JsValue, JsValue[], JsValue> func;
            if (array.Get("join") is ICallable joinFunc)
            {
                func = joinFunc.Call;
            }
            else
            {
                func = _realm.Intrinsics.Object.PrototypeObject.ToObjectString;
            }

            return func(array, Arguments.Empty);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.reduceright
        /// </summary>
        private JsValue ReduceRight(JsValue thisObj, JsValue[] arguments)
        {
            var callbackfn = arguments.At(0);
            var initialValue = arguments.At(1);

            var o = ArrayOperations.For(TypeConverter.ToObject(_realm, thisObj));
            var len = o.GetLongLength();

            var callable = GetCallable(callbackfn);

            if (len == 0 && arguments.Length < 2)
            {
                ExceptionHelper.ThrowTypeError(_realm);
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
                    ExceptionHelper.ThrowTypeError(_realm);
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

        /// <summary>
        /// https://tc39.es/ecma262/#sec-array.prototype.push
        /// </summary>
        public JsValue Push(JsValue thisObject, JsValue[] arguments)
        {
            if (thisObject is ArrayInstance { CanUseFastAccess: true } arrayInstance)
            {
                return arrayInstance.Push(arguments);
            }

            var o = ArrayOperations.For(TypeConverter.ToObject(_realm, thisObject));
            var n = o.GetLongLength();

            if (n + (ulong) arguments.Length > ArrayOperations.MaxArrayLikeLength)
            {
                ExceptionHelper.ThrowTypeError(_realm, "Invalid array length");
            }

            foreach (var a in arguments)
            {
                o.Set(n, a, true);
                n++;
            }

            o.SetLength(n);
            return n;
        }

        public JsValue Pop(JsValue thisObject, JsValue[] arguments)
        {
            var o = ArrayOperations.For(_realm, thisObject);
            ulong len = o.GetLongLength();
            if (len == 0)
            {
                o.SetLength(0);
                return Undefined;
            }

            len = len - 1;
            JsValue element = o.Get(len);
            o.DeletePropertyOrThrow(len);
            o.SetLength(len);
            return element;
        }

        /// <summary>
        /// https://tc39.es/proposal-array-grouping/#sec-array.prototype.group
        /// </summary>
        private JsValue Group(JsValue thisObject, JsValue[] arguments)
        {
            var grouping = BuildArrayGrouping(thisObject, arguments, mapMode: false);

            var obj = OrdinaryObjectCreate(_engine, null);
            foreach (var pair in grouping)
            {
                obj.FastSetProperty(pair.Key, new PropertyDescriptor(pair.Value, PropertyFlag.ConfigurableEnumerableWritable));
            }

            return obj;
        }

        /// <summary>
        /// https://tc39.es/proposal-array-grouping/#sec-array.prototype.grouptomap
        /// </summary>
        private JsValue GroupToMap(JsValue thisObject, JsValue[] arguments)
        {
            var grouping = BuildArrayGrouping(thisObject, arguments, mapMode: true);
            var map = (MapInstance) Construct(_realm.Intrinsics.Map);
            foreach (var pair in grouping)
            {
                map.MapSet(pair.Key, pair.Value);
            }

            return map;
        }

        private Dictionary<JsValue, ArrayInstance> BuildArrayGrouping(JsValue thisObject, JsValue[] arguments, bool mapMode)
        {
            var o = ArrayOperations.For(_realm, thisObject);
            var len = o.GetLongLength();
            var callbackfn = arguments.At(0);
            var callable = GetCallable(callbackfn);
            var thisArg = arguments.At(1);

            var result = new Dictionary<JsValue, ArrayInstance>();
            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = o.Target;
            for (uint k = 0; k < len; k++)
            {
                var kValue = o.Get(k);
                args[0] = kValue;
                args[1] = k;

                var value = callable.Call(thisArg, args);
                JsValue key;
                if (mapMode)
                {
                    key = (value as JsNumber)?.IsNegativeZero() == true ? JsNumber.PositiveZero : value;
                }
                else
                {
                    key = TypeConverter.ToPropertyKey(value);
                }
                if (!result.TryGetValue(key, out var list))
                {
                    result[key] = list = new ArrayInstance(_engine)
                    {
                        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.OnlyWritable)
                    };
                }

                list.SetIndexValue(list.GetLength(), kValue, updateLength: true);
            }

            _engine._jsValueArrayPool.ReturnArray(args);
            return result;
        }

        internal sealed class ArrayComparer : IComparer<JsValue>
        {
            /// <summary>
            /// Default instance without any compare function.
            /// </summary>
            public static readonly ArrayComparer Default = new(null, null);

            public static ArrayComparer WithFunction(Engine engine, ICallable compare)
            {
                if (compare == null)
                {
                    return Default;
                }

                return new ArrayComparer(engine, compare);
            }

            private readonly Engine? _engine;
            private readonly ICallable? _compare;
            private readonly JsValue[] _comparableArray = new JsValue[2];

            private ArrayComparer(Engine? engine, ICallable? compare)
            {
                _engine = engine;
                _compare = compare;
            }

            public int Compare(JsValue x, JsValue y)
            {
                var xIsNull = ReferenceEquals(x, null);
                var yIsNull = ReferenceEquals(y, null);

                if (xIsNull)
                {
                    if (yIsNull)
                    {
                        return 0;
                    }

                    return 1;
                }
                else
                {
                    if (yIsNull)
                    {
                        return -1;
                    }
                }

                var xUndefined = x!.IsUndefined();
                var yUndefined = y!.IsUndefined();
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

                if (_compare != null)
                {
                    _engine!.RunBeforeExecuteStatementChecks(null);

                    _comparableArray[0] = x!;
                    _comparableArray[1] = y!;

                    var s = TypeConverter.ToNumber(_compare.Call(Undefined, _comparableArray));
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

                var xString = TypeConverter.ToString(x!);
                var yString = TypeConverter.ToString(y!);

                return string.CompareOrdinal(xString, yString);
            }
        }
    }
}
