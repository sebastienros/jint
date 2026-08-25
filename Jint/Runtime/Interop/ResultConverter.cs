using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.BigInt;
using Jint.Native.Boolean;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.String;
using Jint.Native.TypedArray;

namespace Jint.Runtime.Interop;

internal static class ResultConverter
{
    public static object? Convert(Engine engine, JsValue value, ResultLimits limits)
    {
        var context = new ConversionContext(engine, limits);
        return context.Convert(value);
    }

    private sealed class ConversionContext
    {
        private readonly Engine _engine;
        private readonly ResultLimits _limits;
        private readonly HashSet<ObjectInstance> _path = new(ReferenceComparer.Instance);
        private long _propertyCount;
        private long _outputCharacters;
        private long _outputBytes;
        private int _depth;
        private int _workUntilConstraintCheck = Engine.ConstraintCheckInterval;

        public ConversionContext(Engine engine, ResultLimits limits)
        {
            _engine = engine;
            _limits = limits;
        }

        public object? Convert(JsValue value)
        {
            if (value is not ObjectInstance instance)
            {
                return ConvertPrimitive(value);
            }

            if (instance is IObjectWrapper wrapper)
            {
                // The target already belongs to the host. Jint neither copies nor walks that CLR graph.
                return wrapper.Target;
            }

            if (instance.IsSpecArray())
            {
                return ConvertArray(instance);
            }

            return ConvertObject(instance);
        }

        private object? ConvertPrimitive(JsValue value)
        {
            if (value.IsString())
            {
                CountStringLength(((JsString) value).Length);
                var text = value.ToString();
                CountOutputCharacters(text.Length);
                return text;
            }

            if (value.IsSymbol())
            {
                Throw.NotSupportedException("Symbol values cannot be converted to a host result.");
            }

            return value.ToObject();
        }

        private object ConvertObject(ObjectInstance instance)
        {
            switch (instance)
            {
                case StringInstance stringInstance:
                    CountStringLength(stringInstance.StringData.Length);
                    var text = stringInstance.StringData.ToString();
                    CountOutputCharacters(text.Length);
                    return text;
                case JsDate date:
                    return date.ToDateTime();
                case BooleanInstance booleanInstance:
                    return booleanInstance.BooleanData._value ? JsBoolean.BoxedTrue : JsBoolean.BoxedFalse;
                case ICallable when instance.Class == ObjectClass.Function:
                    Throw.NotSupportedException("Function values cannot be converted to a host result.");
                    return null!;
                case NumberInstance numberInstance:
                    return numberInstance.NumberData._value;
                case JsRegExp regexp:
                    return regexp.Value;
                case BigIntInstance bigIntInstance:
                    return bigIntInstance.BigIntData._value;
                case JsPromise promise
                    when (_engine.Options.ExperimentalFeatures & ExperimentalFeature.TaskInterop) != ExperimentalFeature.None:
                    return Convert(promise.UnwrapIfPromise(_engine.Options.Constraints.PromiseTimeout))!;
                case JsTypedArray typedArray:
                    return ConvertTypedArray(typedArray);
                case JsArrayBuffer arrayBuffer:
                    arrayBuffer.AssertNotDetached();
                    var arrayBufferData = arrayBuffer.ArrayBufferData!;
                    CountBytes(arrayBufferData.LongLength);
                    return arrayBufferData.AsSpan().ToArray();
                case JsDataView dataView:
                    dataView._viewedArrayBuffer!.AssertNotDetached();
                    CountBytes(dataView._byteLength);
                    var bytes = new byte[dataView._byteLength];
                    System.Array.Copy(
                        dataView._viewedArrayBuffer._arrayBufferData!,
                        dataView._byteOffset,
                        bytes,
                        0,
                        dataView._byteLength);
                    return bytes;
                case JsMap map:
                    return ConvertMap(map);
                case JsSet set:
                    return ConvertSet(set);
                default:
                    return ConvertProperties(instance);
            }
        }

        private object?[] ConvertArray(ObjectInstance array)
        {
            Enter(array);
            try
            {
                var length = array.GetLength();
                CountProperties(length);
                if (length > ClrLimits.MaxArrayLength)
                {
                    ThrowLimit(ResultLimit.PropertyCount, ClrLimits.MaxArrayLength, length);
                }

                var result = new object?[length];
                for (uint i = 0; i < length; i++)
                {
                    CheckConstraints();
                    var value = array[i];
                    result[i] = value.IsUndefined() ? null : Convert(value);
                }

                return result;
            }
            finally
            {
                Exit(array);
            }
        }

        private object ConvertTypedArray(JsTypedArray array)
        {
            Enter(array);
            try
            {
                var length = array.GetLength();
                CountProperties(length);
                if (length > ClrLimits.MaxArrayLength)
                {
                    ThrowLimit(ResultLimit.PropertyCount, ClrLimits.MaxArrayLength, length);
                }

                CountBytes((long) length * array._arrayElementType.GetElementSize());

                return array._arrayElementType switch
                {
                    TypedArrayElementType.Int8 => array.ToNativeArray<sbyte>(),
                    TypedArrayElementType.Int16 => array.ToNativeArray<short>(),
                    TypedArrayElementType.Int32 => array.ToNativeArray<int>(),
                    TypedArrayElementType.BigInt64 => array.ToNativeArray<long>(),
#if SUPPORTS_HALF
                    TypedArrayElementType.Float16 => array.ToNativeArray<Half>(),
#endif
                    TypedArrayElementType.Float32 => array.ToNativeArray<float>(),
                    TypedArrayElementType.Float64 => array.ToNativeArray<double>(),
                    TypedArrayElementType.Uint8 => array.ToNativeArray<byte>(),
                    TypedArrayElementType.Uint8C => array.ToNativeArray<byte>(),
                    TypedArrayElementType.Uint16 => array.ToNativeArray<ushort>(),
                    TypedArrayElementType.Uint32 => array.ToNativeArray<uint>(),
                    TypedArrayElementType.BigUint64 => array.ToNativeArray<ulong>(),
                    _ => throw new NotSupportedException("Cannot handle typed array element type.")
                };
            }
            finally
            {
                Exit(array);
            }
        }

        private List<KeyValuePair<object?, object?>> ConvertMap(JsMap map)
        {
            Enter(map);
            try
            {
                CountProperties(map.Size);
                var result = new List<KeyValuePair<object?, object?>>(map.Size);
                foreach (var pair in map)
                {
                    CheckConstraints();
                    result.Add(new KeyValuePair<object?, object?>(Convert(pair.Key), Convert(pair.Value)));
                }

                return result;
            }
            finally
            {
                Exit(map);
            }
        }

        private object?[] ConvertSet(JsSet set)
        {
            Enter(set);
            try
            {
                CountProperties(set.Size);
                var result = new object?[set.Size];
                var index = 0;
                foreach (var value in set)
                {
                    CheckConstraints();
                    result[index++] = Convert(value);
                }

                return result;
            }
            finally
            {
                Exit(set);
            }
        }

        private Dictionary<string, object?> ConvertProperties(ObjectInstance instance)
        {
            Enter(instance);
            try
            {
                var keys = instance.GetOwnPropertyKeys(Types.String);
                CountProperties(keys.Count);
                var enumerableKeys = new List<JsValue>(keys.Count);
                for (var i = 0; i < keys.Count; i++)
                {
                    CheckConstraints();
                    if (instance.ProbeOwnPropertyChecked(keys[i]) == OwnPropertyProbe.Enumerable)
                    {
                        enumerableKeys.Add(keys[i]);
                    }
                }

                var result = new Dictionary<string, object?>(enumerableKeys.Count, StringComparer.Ordinal);
                for (var i = 0; i < enumerableKeys.Count; i++)
                {
                    CheckConstraints();
                    var key = enumerableKeys[i].ToString();
                    CountStringLength(((JsString) enumerableKeys[i]).Length);
                    CountOutputCharacters(key.Length);
                    result.Add(key, Convert(instance.Get(enumerableKeys[i])));
                }

                return result;
            }
            finally
            {
                Exit(instance);
            }
        }

        private void Enter(ObjectInstance value)
        {
            var nextDepth = _depth + 1;
            if (nextDepth > _limits.MaxDepth)
            {
                ThrowLimit(ResultLimit.Depth, _limits.MaxDepth, nextDepth);
            }

            if (!_path.Add(value))
            {
                Throw.TypeError(_engine.Realm, "Cyclic reference detected.");
            }

            _depth = nextDepth;
        }

        private void Exit(ObjectInstance value)
        {
            _path.Remove(value);
            _depth--;
        }

        private void CountProperties(long count)
        {
            var observed = checked(_propertyCount + count);
            if (observed > _limits.MaxPropertyCount)
            {
                ThrowLimit(ResultLimit.PropertyCount, _limits.MaxPropertyCount, observed);
            }

            _propertyCount = observed;
        }

        private void CountStringLength(int length)
        {
            if (length > _limits.MaxStringLength)
            {
                ThrowLimit(ResultLimit.StringLength, _limits.MaxStringLength, length);
            }
        }

        private void CountOutputCharacters(int length)
        {
            var observed = checked(_outputCharacters + length);
            if (observed > _limits.MaxOutputCharacters)
            {
                ThrowLimit(ResultLimit.OutputCharacters, _limits.MaxOutputCharacters, observed);
            }

            _outputCharacters = observed;
        }

        private void CountBytes(long count)
        {
            var observed = checked(_outputBytes + count);
            if (observed > _limits.MaxOutputBytes)
            {
                ThrowLimit(ResultLimit.OutputBytes, _limits.MaxOutputBytes, observed);
            }

            _outputBytes = observed;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CheckConstraints()
        {
            if (--_workUntilConstraintCheck == 0)
            {
                _engine.Constraints.Check();
                _workUntilConstraintCheck = Engine.ConstraintCheckInterval;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowLimit(ResultLimit limit, long maximum, long observed)
        {
            throw new ResultLimitExceededException(limit, maximum, observed);
        }
    }

    private sealed class ReferenceComparer : IEqualityComparer<ObjectInstance>
    {
        public static ReferenceComparer Instance { get; } = new();

        public bool Equals(ObjectInstance? x, ObjectInstance? y) => ReferenceEquals(x, y);

        public int GetHashCode(ObjectInstance obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
