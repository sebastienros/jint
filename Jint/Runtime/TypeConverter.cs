using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Number;
using Jint.Native.Number.Dtoa;
using Jint.Native.Object;
using Jint.Native.String;
using Jint.Pooling;
using Jint.Runtime.References;

namespace Jint.Runtime
{
    public enum Types
    {
        None = 0,
        Undefined = 1,
        Null = 2,
        Boolean = 3,
        String = 4,
        Number = 5,
        Symbol = 9,
        Object = 10,
        Completion = 20,
    }

    public static class TypeConverter
    {
        // how many decimals to check when determining if double is actually an int
        private const double DoubleIsIntegerTolerance = double.Epsilon * 100;

        private static readonly string[] intToString = new string[1024];
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
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.1
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static JsValue ToPrimitive(JsValue input, Types preferredType = Types.None)
        {
            if (input._type > Types.None && input._type < Types.Object)
            {
                return input;
            }

            return input.AsObject().DefaultValue(preferredType);
        }


        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.2
        /// </summary>
        public static bool ToBoolean(JsValue o)
        {
            switch (o._type)
            {
                case Types.Boolean:
                    return ((JsBoolean) o)._value;
                case Types.Undefined:
                case Types.Null:
                    return false;
                case Types.Number:
                    var n = ((JsNumber) o)._value;
                    return n != 0 && !double.IsNaN(n);
                case Types.String:
                    return !((JsString) o).IsNullOrEmpty();
                default:
                    return true;
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.3
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double ToNumber(JsValue o)
        {
            return o._type == Types.Number
                ? ((JsNumber) o)._value
                : ToNumberUnlikely(o);
        }

        private static double ToNumberUnlikely(JsValue o)
        {
            switch (o._type)
            {
                case Types.Undefined:
                    return double.NaN;
                case Types.Null:
                    return 0;
                case Types.Object when o is IPrimitiveInstance p:
                    return ToNumber(ToPrimitive(p.PrimitiveValue, Types.Number));
                case Types.Boolean:
                    return ((JsBoolean) o)._value ? 1 : 0;
                case Types.String:
                    return ToNumber(o.AsStringWithoutTypeCheck());
                case Types.Symbol:
                    // TODO proper TypeError would require Engine instance and a lot of API changes
                    return ExceptionHelper.ThrowTypeErrorNoEngine<double>("Cannot convert a Symbol value to a number");
                default:
                    return ToNumber(ToPrimitive(o, Types.Number));
            }
        }

        internal static bool CanBeIndex(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            char first = input[0];
            if (first < 32 || (first > 57 && first != 73))
            {
                // does not start with space, +, -, number or I
                return false;
            }

            // might be
            return true;
        }

        private static double ToNumber(string input)
        {
            // eager checks to save time and trimming
            if (string.IsNullOrEmpty(input))
            {
                return 0;
            }

            char first = input[0];
            if (input.Length == 1 && first >= '0' && first <= '9')
            {
                // simple constant number
                return first - '0';
            }

            var s = StringPrototype.IsWhiteSpaceEx(input[0]) || StringPrototype.IsWhiteSpaceEx(input[input.Length - 1])
                ? StringPrototype.TrimEx(input)
                : input;

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
                    int fromBase = 0;
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

                double n = double.Parse(s,
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
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.4
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
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

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.5
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static int ToInt32(JsValue o)
        {
            return (int) (uint) ToNumber(o);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.6
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static uint ToUint32(JsValue o)
        {
            return (uint) ToNumber(o);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.7
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static ushort ToUint16(JsValue o)
        {
            return (ushort) (uint) ToNumber(o);
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
            if (d > long.MinValue && d < long.MaxValue  && Math.Abs(d % 1) <= DoubleIsIntegerTolerance)
            {
                // we are dealing with integer that can be cached
                return ToString((long) d);
            }

            using (var stringBuilder = StringBuilderPool.Rent())
            {
                // we can create smaller array as we know the format to be short
                return NumberPrototype.NumberToString(d, new DtoaBuilder(17), stringBuilder.Builder);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/6.0/#sec-topropertykey
        /// </summary>
        public static string ToPropertyKey(JsValue o)
        {
            var key = ToPrimitive(o, Types.String);
            if (key is JsSymbol s)
            {
                return s._value;
            }

            return ToString(key);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/6.0/#sec-tostring
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static string ToString(JsValue o)
        {
            if (o._type == Types.String)
            {
                return o.AsStringWithoutTypeCheck();
            }

            return ToStringUnlikely(o);
        }

        private static string ToStringUnlikely(JsValue o)
        {
            switch (o._type)
            {
                case Types.Boolean:
                    return ((JsBoolean) o)._value ? "true" : "false";
                case Types.Number:
                    return ToString(((JsNumber) o)._value);
                case Types.Symbol:
                    return ExceptionHelper.ThrowTypeErrorNoEngine<string>("Cannot convert a Symbol value to a string");
                case Types.Undefined:
                    return Undefined.Text;
                case Types.Null:
                    return Null.Text;
                case Types.Object when o is IPrimitiveInstance p:
                    return ToString(ToPrimitive(p.PrimitiveValue, Types.String));
                default:
                    return ToString(ToPrimitive(o, Types.String));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ObjectInstance ToObject(Engine engine, JsValue value)
        {
            switch (value._type)
            {
                case Types.Object:
                    return (ObjectInstance) value;
                case Types.Boolean:
                    return engine.Boolean.Construct(((JsBoolean) value)._value);
                case Types.Number:
                    return engine.Number.Construct(((JsNumber) value)._value);
                case Types.String:
                    return engine.String.Construct(value.AsStringWithoutTypeCheck());
                case Types.Symbol:
                    return engine.Symbol.Construct(((JsSymbol) value)._value);
                default:
                    ExceptionHelper.ThrowTypeError(engine);
                    return null;
            }
        }

        public static Types GetPrimitiveType(JsValue value)
        {
            if (value._type == Types.Object)
            {
                if (value is IPrimitiveInstance primitive)
                {
                    return primitive.Type;
                }

                return Types.Object;
            }

            return value.Type;
        }

        public static void CheckObjectCoercible(
            Engine engine,
            JsValue o,
            MemberExpression expression,
            object baseReference)
        {
            if (o._type > Types.Null)
            {
                return;
            }

            var referenceResolver = engine.Options.ReferenceResolver;
            if (referenceResolver != null && referenceResolver.CheckCoercible(o))
            {
                return;
            }

            ThrowTypeError(engine, o, expression, baseReference);
        }

        internal static void CheckObjectCoercible(
            Engine engine,
            JsValue o,
            MemberExpression expression,
            string referenceName)
        {
            if (o._type > Types.Null)
            {
                return;
            }

            var referenceResolver = engine.Options.ReferenceResolver;
            if (referenceResolver != null && referenceResolver.CheckCoercible(o))
            {
                return;
            }

            ThrowTypeError(engine, o, expression, referenceName);
        }

        private static void ThrowTypeError(
            Engine engine,
            JsValue o,
            MemberExpression expression,
            object baseReference)
        {
            ThrowTypeError(engine, o, expression, (baseReference as Reference)?.GetReferencedName());
        }

        private static void ThrowTypeError(
            Engine engine,
            JsValue o,
            MemberExpression expression,
            string referencedName)
        {
            referencedName = referencedName ?? "The value";
            var message = $"{referencedName} is {o}";
            throw new JavaScriptException(engine.TypeError, message).SetCallstack(engine, expression.Location);
        }

        public static void CheckObjectCoercible(Engine engine, JsValue o)
        {
            if (o._type == Types.Undefined || o._type == Types.Null)
            {
                ExceptionHelper.ThrowTypeError(engine);
            }
        }

        public static IEnumerable<Tuple<MethodBase, JsValue[]>> FindBestMatch<T>(Engine engine, T[] methods, Func<T, bool, JsValue[]> argumentProvider) where T : MethodBase
        {
            System.Collections.Generic.List<Tuple<T, JsValue[]>> matchingByParameterCount = null;
            foreach (var m in methods)
            {
                bool hasParams = false;
                var parameterInfos = m.GetParameters();
                foreach (var parameter in parameterInfos)
                {
                    if (Attribute.IsDefined(parameter, typeof(ParamArrayAttribute)))
                    {
                        hasParams = true;
                        break;
                    }
                }

                var arguments = argumentProvider(m, hasParams);
                if (parameterInfos.Length == arguments.Length)
                {
                    if (methods.Length == 0 && arguments.Length == 0)
                    {
                        yield return new Tuple<MethodBase, JsValue[]>(m, arguments);
                        yield break;
                    }

                    matchingByParameterCount = matchingByParameterCount ?? new System.Collections.Generic.List<Tuple<T, JsValue[]>>();
                    matchingByParameterCount.Add(new Tuple<T, JsValue[]>(m, arguments));
                }
                else if (parameterInfos.Length > arguments.Length)
                {
                    // check if we got enough default values to provide all parameters (or more in case some default values are provided/overwritten)
                    var defaultValuesCount = 0;
                    foreach (var param in parameterInfos)
                    {
                        if (param.HasDefaultValue) defaultValuesCount++;
                    }

                    if (parameterInfos.Length <= arguments.Length + defaultValuesCount)
                    {
                        // create missing arguments from default values

                        var argsWithDefaults = new System.Collections.Generic.List<JsValue>(arguments);
                        for (var i = arguments.Length; i < parameterInfos.Length; i++)
                        {
                            var param = parameterInfos[i];
                            var value = JsValue.FromObject(engine, param.DefaultValue);
                            argsWithDefaults.Add(value);
                        }

                        matchingByParameterCount = matchingByParameterCount ?? new System.Collections.Generic.List<Tuple<T, JsValue[]>>();
                        matchingByParameterCount.Add(new Tuple<T, JsValue[]>(m, argsWithDefaults.ToArray()));
                    }
                }
            }

            if (matchingByParameterCount == null)
            {
                yield break;
            }

            foreach (var tuple in matchingByParameterCount)
            {
                var perfectMatch = true;
                var parameters = tuple.Item1.GetParameters();
                var arguments = tuple.Item2;
                for (var i = 0; i < arguments.Length; i++)
                {
                    var arg = arguments[i].ToObject();
                    var paramType = parameters[i].ParameterType;
                    if (arg == null)
                    {
                        if (!TypeIsNullable(paramType))
                        {
                            perfectMatch = false;
                            break;
                        }
                    }
                    else if (arg.GetType() != paramType)
                    {
                        perfectMatch = false;
                        break;
                    }
                }

                if (perfectMatch)
                {
                    yield return new Tuple<MethodBase, JsValue[]>(tuple.Item1, arguments);
                    yield break;
                }
            }

            for (var i = 0; i < matchingByParameterCount.Count; i++)
            {
                var tuple = matchingByParameterCount[i];
                yield return new Tuple<MethodBase, JsValue[]>(tuple.Item1, tuple.Item2);
            }
        }

        public static bool TypeIsNullable(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }
    }
}
