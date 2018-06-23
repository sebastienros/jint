using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Esprima.Ast;
using Jint.Native;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.String;
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
        public static double ToNumber(JsValue o)
        {
            switch (o._type)
            {
                // check number first as this is what is usually expected
                case Types.Number:
                    return ((JsNumber) o)._value;
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
                if (!s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
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

                int i = int.Parse(s.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

                return i;
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
            return i >= 0 && i < intToString.Length
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
        internal static string ToString(double d)
        {
            if (d > long.MinValue && d < long.MaxValue  && Math.Abs(d % 1) <= DoubleIsIntegerTolerance)
            {
                // we are dealing with integer that can be cached
                return ToString((long) d);
            }

            return NumberPrototype.ToNumberString(d);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.8
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static string ToString(JsValue o)
        {
            switch (o._type)
            {
                case Types.String:
                    return o.AsStringWithoutTypeCheck();
                case Types.Boolean:
                    return ((JsBoolean) o)._value ? "true" : "false";
                case Types.Number:
                    return ToString(((JsNumber) o)._value);
                case Types.Symbol:
                    return o.AsSymbol();
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
            if (o._type != Types.Undefined && o._type != Types.Null)
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

        private static void ThrowTypeError(Engine engine, JsValue o, MemberExpression expression, object baseReference)
        {
            var referencedName = "The value";
            if (baseReference is Reference reference)
            {
                referencedName = reference.GetReferencedName();
            }
            
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

        public static IEnumerable<MethodBase> FindBestMatch(Engine engine, MethodBase[] methods, JsValue[] arguments)
        {
            methods = methods
                .Where(m => m.GetParameters().Length == arguments.Length)
                .ToArray();

            if (methods.Length == 1 && !methods[0].GetParameters().Any())
            {
                yield return methods[0];
                yield break;
            }

            var objectArguments = arguments.Select(x => x.ToObject()).ToArray();
            foreach (var method in methods)
            {
                var perfectMatch = true;
                var parameters = method.GetParameters();
                for (var i = 0; i < arguments.Length; i++)
                {
                    var arg = objectArguments[i];
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
                    yield return method;
                    yield break;
                }
            }

            foreach (var method in methods)
            {
                yield return method;
            }
        }

        public static bool TypeIsNullable(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }
    }
}
