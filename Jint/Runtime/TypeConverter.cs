using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Extensions;
using Jint.Native;
using Jint.Native.Number;
using Jint.Native.Number.Dtoa;
using Jint.Native.Object;
using Jint.Native.String;
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime.Interop;

namespace Jint.Runtime
{
    [Flags]
    public enum Types
    {
        None = 0,
        Undefined = 1,
        Null = 2,
        Boolean = 4,
        String = 8,
        Number = 16,
        Symbol = 64,
        Object = 128
    }

    [Flags]
    internal enum InternalTypes
    {
        // should not be used, used for empty match
        None = 0,

        Undefined = 1,
        Null = 2,

        // primitive  types range start
        Boolean = 4,
        String = 8,
        Number = 16,
        Integer = 32,
        Symbol = 64,

        // primitive  types range end
        Object = 128,

        // internal usage
        ObjectEnvironmentRecord = 512,
        RequiresCloning = 1024,

        Primitive = Boolean | String | Number | Integer | Symbol,
        InternalFlags = ObjectEnvironmentRecord | RequiresCloning
    }

    public static class TypeConverter
    {
        // how many decimals to check when determining if double is actually an int
        private const double DoubleIsIntegerTolerance = double.Epsilon * 100;

        internal static readonly string[] intToString = new string[1024];
        private static readonly string[] charToString = new string[256];

        static TypeConverter()
        {
            for (var i = 0; i < intToString.Length; ++i)
            {
                intToString[i] = i.ToString();
            }

            for (var i = 0; i < charToString.Length; ++i)
            {
                var c = (char) i;
                charToString[i] = c.ToString();
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-toprimitive
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsValue ToPrimitive(JsValue input, Types preferredType = Types.None)
        {
            return input is not ObjectInstance oi
                ? input
                : ToPrimitiveObjectInstance(oi, preferredType);
        }

        private static JsValue ToPrimitiveObjectInstance(ObjectInstance oi, Types preferredType)
        {
            var hint = preferredType switch
            {
                Types.String => JsString.StringString,
                Types.Number => JsString.NumberString,
                _ => JsString.DefaultString
            };

            var exoticToPrim = oi.GetMethod(GlobalSymbolRegistry.ToPrimitive);
            if (exoticToPrim is object)
            {
                var str = exoticToPrim.Call(oi, new JsValue[] { hint });
                if (str.IsPrimitive())
                {
                    return str;
                }

                if (str.IsObject())
                {
                    ExceptionHelper.ThrowTypeError(oi.Engine.Realm, "Cannot convert object to primitive value");
                }
            }

            return OrdinaryToPrimitive(oi, preferredType == Types.None ? Types.Number : preferredType);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ordinarytoprimitive
        /// </summary>
        internal static JsValue OrdinaryToPrimitive(ObjectInstance input, Types hint = Types.None)
        {
            JsString property1;
            JsString property2;

            if (hint == Types.String)
            {
                property1 = (JsString) "toString";
                property2 = (JsString) "valueOf";
            }
            else if (hint == Types.Number)
            {
                property1 = (JsString) "valueOf";
                property2 = (JsString) "toString";
            }
            else
            {
                ExceptionHelper.ThrowTypeError(input.Engine.Realm);
                return null;
            }

            if (input.Get(property1) is ICallable method1)
            {
                var val = method1.Call(input, Arguments.Empty);
                if (val.IsPrimitive())
                {
                    return val;
                }
            }

            if (input.Get(property2) is ICallable method2)
            {
                var val = method2.Call(input, Arguments.Empty);
                if (val.IsPrimitive())
                {
                    return val;
                }
            }

            ExceptionHelper.ThrowTypeError(input.Engine.Realm);
            return null;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-toboolean
        /// </summary>
        public static bool ToBoolean(JsValue o)
        {
            var type = o._type & ~InternalTypes.InternalFlags;
            switch (type)
            {
                case InternalTypes.Boolean:
                    return ((JsBoolean) o)._value;
                case InternalTypes.Undefined:
                case InternalTypes.Null:
                    return false;
                case InternalTypes.Integer:
                    return (int) ((JsNumber) o)._value != 0;
                case InternalTypes.Number:
                    var n = ((JsNumber) o)._value;
                    return n != 0 && !double.IsNaN(n);
                case InternalTypes.String:
                    return !((JsString) o).IsNullOrEmpty();
                default:
                    return true;
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-tonumber
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ToNumber(JsValue o)
        {
            return o.IsNumber()
                ? ((JsNumber) o)._value
                : ToNumberUnlikely(o);
        }

        private static double ToNumberUnlikely(JsValue o)
        {
            var type = o._type & ~InternalTypes.InternalFlags;

            switch (type)
            {
                case InternalTypes.Undefined:
                    return double.NaN;
                case InternalTypes.Null:
                    return 0;
                case InternalTypes.Object when o is IPrimitiveInstance p:
                    return ToNumber(ToPrimitive(p.PrimitiveValue, Types.Number));
                case InternalTypes.Boolean:
                    return ((JsBoolean) o)._value ? 1 : 0;
                case InternalTypes.String:
                    return ToNumber(o.ToString());
                case InternalTypes.Symbol:
                    // TODO proper TypeError would require Engine instance and a lot of API changes
                    ExceptionHelper.ThrowTypeErrorNoEngine("Cannot convert a Symbol value to a number");
                    return 0;
                default:
                    return ToNumber(ToPrimitive(o, Types.Number));
            }
        }

        private static double ToNumber(string input)
        {
            // eager checks to save time and trimming
            if (string.IsNullOrEmpty(input))
            {
                return 0;
            }

            var first = input[0];
            if (input.Length == 1 && first >= '0' && first <= '9')
            {
                // simple constant number
                return first - '0';
            }

            var s = StringPrototype.TrimEx(input);

            if (s.Length == 0)
            {
                return 0;
            }

            if (s.Length == 8 || s.Length == 9)
            {
                if ("+Infinity" == s || "Infinity" == s)
                {
                    return double.PositiveInfinity;
                }

                if ("-Infinity" == s)
                {
                    return double.NegativeInfinity;
                }
            }

            // todo: use a common implementation with JavascriptParser
            try
            {
                if (s.Length > 2 && s[0] == '0' && char.IsLetter(s[1]))
                {
                    var fromBase = 0;
                    if (s[1] == 'x' || s[1] == 'X')
                    {
                        fromBase = 16;
                    }

                    if (s[1] == 'o' || s[1] == 'O')
                    {
                        fromBase = 8;
                    }

                    if (s[1] == 'b' || s[1] == 'B')
                    {
                        fromBase = 2;
                    }

                    if (fromBase > 0)
                    {
                        return Convert.ToInt32(s.Substring(2), fromBase);
                    }
                }

                var start = s[0];
                if (start != '+' && start != '-' && start != '.' && !char.IsDigit(start))
                {
                    return double.NaN;
                }

                var n = double.Parse(s,
                    NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign |
                    NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
                    NumberStyles.AllowExponent, CultureInfo.InvariantCulture);
                if (s.StartsWith("-") && n == 0)
                {
                    return -0.0;
                }

                return n;
            }
            catch (OverflowException)
            {
                return s.StartsWith("-") ? double.NegativeInfinity : double.PositiveInfinity;
            }
            catch
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-tolength
        /// </summary>
        public static ulong ToLength(JsValue o)
        {
            var len = ToInteger(o);
            if (len <= 0)
            {
                return 0;
            }

            return (ulong) Math.Min(len, NumberConstructor.MaxSafeInteger);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-tointegerorinfinity
        /// </summary>
        public static double ToIntegerOrInfinity(JsValue argument)
        {
            var number = ToNumber(argument);
            if (double.IsNaN(number) || number == 0)
            {
                return 0;
            }

            if (double.IsInfinity(number))
            {
                return number;
            }

            var integer = (long) Math.Floor(Math.Abs(number));
            if (number < 0)
            {
                integer *= -1;
            }

            return integer;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-tointeger
        /// </summary>
        public static double ToInteger(JsValue o)
        {
            var number = ToNumber(o);

            if (double.IsNaN(number))
            {
                return 0;
            }

            if (number == 0 || double.IsInfinity(number))
            {
                return number;
            }

            return (long) number;
        }

        internal static double ToInteger(string o)
        {
            var number = ToNumber(o);

            if (double.IsNaN(number))
            {
                return 0;
            }

            if (number == 0 || double.IsInfinity(number))
            {
                return number;
            }

            return (long) number;
        }

        internal static int DoubleToInt32Slow(double o)
        {
            // Computes the integral value of the number mod 2^32.

            var doubleBits = BitConverter.DoubleToInt64Bits(o);
            var sign = (int) (doubleBits >> 63); // 0 if positive, -1 if negative
            var exponent = (int) ((doubleBits >> 52) & 0x7FF) - 1023;

            if ((uint) exponent >= 84)
            {
                // Anything with an exponent that is negative or >= 84 will convert to zero.
                // This includes infinities and NaNs, which have exponent = 1024
                // The 84 comes from 52 (bits in double mantissa) + 32 (bits in integer)
                return 0;
            }

            var mantissa = (doubleBits & 0xFFFFFFFFFFFFFL) | 0x10000000000000L;
            var int32Value = exponent >= 52 ? (int) (mantissa << (exponent - 52)) : (int) (mantissa >> (52 - exponent));

            return (int32Value + sign) ^ sign;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.5
        /// </summary>
        public static int ToInt32(JsValue o)
        {
            if (o._type == InternalTypes.Integer)
            {
                return o.AsInteger();
            }

            var doubleVal = ToNumber(o);
            if (doubleVal >= -(double) int.MinValue && doubleVal <= int.MaxValue)
            {
                // Double-to-int cast is correct in this range
                return (int) doubleVal;
            }

            return DoubleToInt32Slow(doubleVal);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.6
        /// </summary>
        public static uint ToUint32(JsValue o)
        {
            if (o._type == InternalTypes.Integer)
            {
                return (uint) o.AsInteger();
            }

            var doubleVal = ToNumber(o);
            if (doubleVal >= 0.0 && doubleVal <= uint.MaxValue)
            {
                // Double-to-uint cast is correct in this range
                return (uint) doubleVal;
            }

            return (uint) DoubleToInt32Slow(doubleVal);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.7
        /// </summary>
        public static ushort ToUint16(JsValue o)
        {
            return o._type == InternalTypes.Integer
                ? (ushort) (uint) o.AsInteger()
                : (ushort) (uint) ToNumber(o);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-toint16
        /// </summary>
        internal static double ToInt16(JsValue o)
        {
            return o._type == InternalTypes.Integer
                ? (short) o.AsInteger()
                : (short) (long) ToNumber(o);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-toint8
        /// </summary>
        internal static double ToInt8(JsValue o)
        {
            return o._type == InternalTypes.Integer
                ? (sbyte) o.AsInteger()
                : (sbyte) (long) ToNumber(o);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-touint8
        /// </summary>
        internal static double ToUint8(JsValue o)
        {
            return o._type == InternalTypes.Integer
                ? (byte) o.AsInteger()
                : (byte) (long) ToNumber(o);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-touint8clamp
        /// </summary>
        internal static byte ToUint8Clamp(JsValue o)
        {
            if (o._type == InternalTypes.Integer)
            {
                var intValue = o.AsInteger();
                if (intValue is > -1 and < 256)
                {
                    return (byte) intValue;
                }
            }

            return ToUint8ClampUnlikely(o);
        }

        private static byte ToUint8ClampUnlikely(JsValue o)
        {
            var number = ToNumber(o);
            if (double.IsNaN(number))
            {
                return 0;
            }

            if (number <= 0)
            {
                return 0;
            }

            if (number >= 255)
            {
                return 255;
            }

            var f = Math.Floor(number);
            if (f + 0.5 < number)
            {
                return (byte) (f + 1);
            }

            if (number < f + 0.5)
            {
                return (byte) f;
            }

            if (f % 2 != 0)
            {
                return (byte) (f + 1);
            }

            return (byte) f;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-tobigint
        /// </summary>
        public static double ToBigInt(JsValue value)
        {
            ExceptionHelper.ThrowNotImplementedException("BigInt not implemented");
            return 0;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-tobigint64
        /// </summary>
        internal static double ToBigInt64(JsValue value)
        {
            ExceptionHelper.ThrowNotImplementedException("BigInt not implemented");
            return 0;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-tobiguint64
        /// </summary>
        internal static double ToBigUint64(JsValue value)
        {
            ExceptionHelper.ThrowNotImplementedException("BigInt not implemented");
            return 0;
        }


        /// <summary>
        /// https://tc39.es/ecma262/#sec-canonicalnumericindexstring
        /// </summary>
        internal static double? CanonicalNumericIndexString(JsValue value)
        {
            if (value is JsNumber jsNumber)
            {
                return jsNumber._value;
            }

            if (value is JsString jsString)
            {
                if (jsString.ToString() == "-0")
                {
                    return -0d;
                }

                var n = ToNumber(value);
                if (!JsValue.SameValue(ToString(n), value))
                {
                    return null;
                }

                return n;
            }

            return null;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-toindex
        /// </summary>
        public static uint ToIndex(Realm realm, JsValue value)
        {
            if (value.IsUndefined())
            {
                return 0;
            }

            var integerIndex = ToIntegerOrInfinity(value);
            if (integerIndex < 0)
            {
                ExceptionHelper.ThrowRangeError(realm);
            }

            var index = ToLength(integerIndex);
            if (integerIndex != index)
            {
                ExceptionHelper.ThrowRangeError(realm, "Invalid index");
            }

            return (uint) Math.Min(uint.MaxValue, index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToString(long i)
        {
            return i >= 0 && i < intToString.Length
                ? intToString[i]
                : i.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToString(int i)
        {
            return i >= 0 && i < intToString.Length
                ? intToString[i]
                : i.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToString(uint i)
        {
            return i < (uint) intToString.Length
                ? intToString[i]
                : i.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToString(char c)
        {
            return c >= 0 && c < charToString.Length
                ? charToString[c]
                : c.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToString(ulong i)
        {
            return i >= 0 && i < (ulong) intToString.Length
                ? intToString[i]
                : i.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static string ToString(double d)
        {
            if (d > long.MinValue && d < long.MaxValue && Math.Abs(d % 1) <= DoubleIsIntegerTolerance)
            {
                // we are dealing with integer that can be cached
                return ToString((long) d);
            }

            using var stringBuilder = StringBuilderPool.Rent();
            // we can create smaller array as we know the format to be short
            return NumberPrototype.NumberToString(d, new DtoaBuilder(17), stringBuilder.Builder);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/6.0/#sec-topropertykey
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsValue ToPropertyKey(JsValue o)
        {
            const InternalTypes stringOrSymbol = InternalTypes.String | InternalTypes.Symbol;
            return (o._type & stringOrSymbol) != 0
                ? o
                : ToPropertyKeyNonString(o);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static JsValue ToPropertyKeyNonString(JsValue o)
        {
            const InternalTypes stringOrSymbol = InternalTypes.String | InternalTypes.Symbol;
            var primitive = ToPrimitive(o, Types.String);
            return (primitive._type & stringOrSymbol) != 0
                ? primitive
                : ToStringNonString(primitive);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/6.0/#sec-tostring
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToString(JsValue o)
        {
            return o.IsString() ? o.ToString() : ToStringNonString(o);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static JsString ToJsString(JsValue o)
        {
            if (o is JsString s)
            {
                return s;
            }

            return JsString.Create(ToStringNonString(o));
        }

        private static string ToStringNonString(JsValue o)
        {
            var type = o._type & ~InternalTypes.InternalFlags;
            switch (type)
            {
                case InternalTypes.Boolean:
                    return ((JsBoolean) o)._value ? "true" : "false";
                case InternalTypes.Integer:
                    return ToString((int) ((JsNumber) o)._value);
                case InternalTypes.Number:
                    return ToString(((JsNumber) o)._value);
                case InternalTypes.Symbol:
                    ExceptionHelper.ThrowTypeErrorNoEngine("Cannot convert a Symbol value to a string");
                    return null;
                case InternalTypes.Undefined:
                    return Undefined.Text;
                case InternalTypes.Null:
                    return Null.Text;
                case InternalTypes.Object when o is IPrimitiveInstance p:
                    return ToString(ToPrimitive(p.PrimitiveValue, Types.String));
                case InternalTypes.Object when o is IObjectWrapper p:
                    return p.Target?.ToString();
                default:
                    return ToString(ToPrimitive(o, Types.String));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ObjectInstance ToObject(Realm realm, JsValue value)
        {
            if (value is ObjectInstance oi)
            {
                return oi;
            }

            return ToObjectNonObject(realm, value);
        }

        private static ObjectInstance ToObjectNonObject(Realm realm, JsValue value)
        {
            var type = value._type & ~InternalTypes.InternalFlags;
            switch (type)
            {
                case InternalTypes.Boolean:
                    return realm.Intrinsics.Boolean.Construct((JsBoolean) value);
                case InternalTypes.Number:
                case InternalTypes.Integer:
                    return realm.Intrinsics.Number.Construct((JsNumber) value);
                case InternalTypes.String:
                    return realm.Intrinsics.String.Construct(value.ToString());
                case InternalTypes.Symbol:
                    return realm.Intrinsics.Symbol.Construct((JsSymbol) value);
                case InternalTypes.Null:
                case InternalTypes.Undefined:
                    ExceptionHelper.ThrowTypeError(realm, "Cannot convert undefined or null to object");
                    return null;
                default:
                    ExceptionHelper.ThrowTypeError(realm, "Cannot convert given item to object");
                    return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckObjectCoercible(
            Engine engine,
            JsValue o,
            Node sourceNode,
            string referenceName)
        {
            if (!engine._referenceResolver.CheckCoercible(o))
            {
                ThrowMemberNullOrUndefinedError(engine, o, sourceNode, referenceName);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowMemberNullOrUndefinedError(
            Engine engine,
            JsValue o,
            Node sourceNode,
            string referencedName)
        {
            referencedName ??= "unknown";
            var message = $"Cannot read property '{referencedName}' of {o}";
            throw new JavaScriptException(engine.Realm.Intrinsics.TypeError, message).SetCallstack(engine, sourceNode.Location);
        }

        public static void CheckObjectCoercible(Engine engine, JsValue o)
        {
            if (o._type < InternalTypes.Boolean)
            {
                ExceptionHelper.ThrowTypeError(engine.Realm);
            }
        }

        internal readonly record struct MethodMatch(MethodDescriptor Method, JsValue[] Arguments, int Score = 0) : IComparable<MethodMatch>
        {
            public int CompareTo(MethodMatch other) => Score.CompareTo(other.Score);
        }

        internal static IEnumerable<MethodMatch> FindBestMatch(
            Engine engine,
            MethodDescriptor[] methods,
            Func<MethodDescriptor, JsValue[]> argumentProvider)
        {
            List<MethodMatch> matchingByParameterCount = null;
            foreach (var method in methods)
            {
                var parameterInfos = method.Parameters;
                var arguments = argumentProvider(method);
                if (arguments.Length <= parameterInfos.Length
                    && arguments.Length >= parameterInfos.Length - method.ParameterDefaultValuesCount)
                {
                    var score = CalculateMethodScore(engine, method, arguments);
                    if (score == 0)
                    {
                        // perfect match
                        yield return new MethodMatch(method, arguments);
                        yield break;
                    }

                    if (score < 0)
                    {
                        // discard
                        continue;
                    }

                    matchingByParameterCount ??= new List<MethodMatch>();
                    matchingByParameterCount.Add(new MethodMatch(method, arguments, score));
                }
            }

            if (matchingByParameterCount == null)
            {
                yield break;
            }

            if (matchingByParameterCount.Count > 1)
            {
                matchingByParameterCount.Sort();
            }

            foreach (var match in matchingByParameterCount)
            {
                yield return match;
            }
        }

        /// <summary>
        /// Method's match score tells how far away it's from ideal candidate. 0 = ideal, bigger the the number,
        /// the farther away the candidate is from ideal match. Negative signals impossible match.
        /// </summary>
        private static int CalculateMethodScore(Engine engine, MethodDescriptor method, JsValue[] arguments)
        {
            if (method.Parameters.Length == 0 && arguments.Length == 0)
            {
                // perfect
                return 0;
            }

            var score = 0;
            for (var i = 0; i < arguments.Length; i++)
            {
                var jsValue = arguments[i];
                var paramType = method.Parameters[i].ParameterType;

                var parameterScore = CalculateMethodParameterScore(engine, jsValue, paramType);
                if (parameterScore < 0)
                {
                    return parameterScore;
                }
                score += parameterScore;
            }

            return score;
        }

        /// <summary>
        /// Determines how well parameter type matches target method's type.
        /// </summary>
        private static int CalculateMethodParameterScore(
            Engine engine,
            JsValue jsValue,
            Type paramType)
        {
            var objectValue = jsValue.ToObject();
            var objectValueType = objectValue?.GetType();

            if (objectValueType == paramType)
            {
                return 0;
            }

            if (objectValue == null)
            {
                if (!TypeIsNullable(paramType))
                {
                    // this is bad
                    return -1;
                }

                return 0;
            }

            if (paramType == typeof(JsValue))
            {
                // JsValue is convertible to. But it is still not a perfect match
                return 1;
            }

            if (paramType == typeof(object))
            {
                // a catch-all, prefer others over it
                return 5;
            }

            if (paramType.IsEnum &&
                jsValue is JsNumber jsNumber
                && jsNumber.IsInteger()
                && Enum.IsDefined(paramType, jsNumber.AsInteger()))
            {
                // we can do conversion from int value to enum
                return 0;
            }

            if (paramType.IsAssignableFrom(objectValueType))
            {
                // is-a-relation
                return 1;
            }

            if (jsValue.IsArray() && objectValueType.IsArray)
            {
                // we have potential, TODO if we'd know JS array's internal type we could have exact match
                return 2;
            }

            if (CanChangeType(objectValue, paramType))
            {
                // forcing conversion isn't ideal not ideal, but works, especially for int -> double for example
                return 1;
            }

            if (engine.Options.Interop.AllowOperatorOverloading)
            {
                foreach (var m in objectValueType.GetOperatorOverloadMethods())
                {
                    if (paramType.IsAssignableFrom(m.ReturnType) && m.Name is "op_Implicit" or "op_Explicit")
                    {
                        // implicit/explicit operator conversion is OK, but not ideal
                        return 1;
                    }
                }
            }

            if (ReflectionExtensions.TryConvertViaTypeCoercion(paramType, engine.Options.Interop.ValueCoercion, jsValue, out _))
            {
                // gray JS zone where we start to do odd things
                return 10;
            }

            // will rarely succeed
            return 100;
        }

        private static bool CanChangeType(object value, Type targetType)
        {
            if (value is null && !targetType.IsValueType)
            {
                return true;
            }

            if (value is not IConvertible)
            {
                return false;
            }

            try
            {
                Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                // nope
                return false;
            }
        }

        internal static bool TypeIsNullable(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }
    }
}