using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using Esprima;
using Esprima.Ast;
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
        public static JsValue ToPrimitive(JsValue input, Types preferredType = Types.None)
        {
            if (!(input is ObjectInstance oi))
            {
                return input;
            }

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
                    return ExceptionHelper.ThrowTypeError<JsValue>(oi.Engine, "Cannot convert object to primitive value");
                }
            }

            return OrdinaryToPrimitive(oi, preferredType == Types.None ? Types.Number :  preferredType);
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
                return ExceptionHelper.ThrowTypeError<JsValue>(input.Engine);
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

            return ExceptionHelper.ThrowTypeError<JsValue>(input.Engine);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.2
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
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.3
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
            return type switch
            {
                InternalTypes.Undefined => double.NaN,
                InternalTypes.Null => 0,
                InternalTypes.Object when o is IPrimitiveInstance p => ToNumber(ToPrimitive(p.PrimitiveValue, Types.Number)),
                InternalTypes.Boolean => (((JsBoolean) o)._value ? 1 : 0),
                InternalTypes.String => ToNumber(o.ToString()),
                InternalTypes.Symbol =>
                // TODO proper TypeError would require Engine instance and a lot of API changes
                ExceptionHelper.ThrowTypeErrorNoEngine<double>("Cannot convert a Symbol value to a number"),
                _ => ToNumber(ToPrimitive(o, Types.Number))
            };
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

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.5
        /// </summary>
        public static int ToInt32(JsValue o)
        {
            return o._type == InternalTypes.Integer
                ? o.AsInteger()
                : (int) (uint) ToNumber(o);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.6
        /// </summary>
        public static uint ToUint32(JsValue o)
        {
            return o._type == InternalTypes.Integer
                ? (uint) o.AsInteger()
                : (uint) ToNumber(o);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.7
        /// </summary>
        public static ushort ToUint16(JsValue o)
        {
            return  o._type == InternalTypes.Integer
                ? (ushort) (uint) o.AsInteger()
                : (ushort) (uint) ToNumber(o);
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
            return type switch
            {
                InternalTypes.Boolean => ((JsBoolean) o)._value ? "true" : "false",
                InternalTypes.Integer => ToString((int) ((JsNumber) o)._value),
                InternalTypes.Number => ToString(((JsNumber) o)._value),
                InternalTypes.Symbol => ExceptionHelper.ThrowTypeErrorNoEngine<string>("Cannot convert a Symbol value to a string"),
                InternalTypes.Undefined => Undefined.Text,
                InternalTypes.Null => Null.Text,
                InternalTypes.Object when o is IPrimitiveInstance p => ToString(ToPrimitive(p.PrimitiveValue, Types.String)),
                InternalTypes.Object when o is IObjectWrapper p => p.Target?.ToString(),
                _ => ToString(ToPrimitive(o, Types.String))
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ObjectInstance ToObject(Engine engine, JsValue value)
        {
            if (value is ObjectInstance oi)
            {
                return oi;
            }

            return ToObjectNonObject(engine, value);
        }

        private static ObjectInstance ToObjectNonObject(Engine engine, JsValue value)
        {
            var type = value._type & ~InternalTypes.InternalFlags;
            return type switch
            {
                InternalTypes.Boolean => engine.Boolean.Construct((JsBoolean) value),
                InternalTypes.Number => engine.Number.Construct((JsNumber) value),
                InternalTypes.Integer => engine.Number.Construct((JsNumber) value),
                InternalTypes.String => engine.String.Construct(value.ToString()),
                InternalTypes.Symbol => engine.Symbol.Construct((JsSymbol) value),
                InternalTypes.Null => ExceptionHelper.ThrowTypeError<ObjectInstance>(engine, "Cannot convert undefined or null to object"),
                InternalTypes.Undefined => ExceptionHelper.ThrowTypeError<ObjectInstance>(engine, "Cannot convert undefined or null to object"),
                _ => ExceptionHelper.ThrowTypeError<ObjectInstance>(engine, "Cannot convert given item to object")
            };
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void CheckObjectCoercible(
            Engine engine,
            JsValue o,
            Node sourceNode,
            string referenceName)
        {
            if (!engine.Options.ReferenceResolver.CheckCoercible(o))
            {
                ThrowMemberNullOrUndefinedError(engine, o, sourceNode.Location, referenceName);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ThrowMemberNullOrUndefinedError(
            Engine engine,
            JsValue o,
            in Location location,
            string referencedName)
        {
            referencedName ??= "unknown";
            var message = $"Cannot read property '{referencedName}' of {o}";
            throw new JavaScriptException(engine.TypeError, message).SetCallstack(engine, location);
        }

        public static void CheckObjectCoercible(Engine engine, JsValue o)
        {
            if (o._type < InternalTypes.Boolean)
            {
                ExceptionHelper.ThrowTypeError(engine);
            }
        }

        internal static IEnumerable<Tuple<MethodDescriptor, JsValue[]>> FindBestMatch(
            Engine engine,
            MethodDescriptor[] methods,
            Func<MethodDescriptor, JsValue[]> argumentProvider)
        {
            List<Tuple<MethodDescriptor, JsValue[]>> matchingByParameterCount = null;
            foreach (var m in methods)
            {
                var parameterInfos = m.Parameters;
                var arguments = argumentProvider(m);
                if (arguments.Length <= parameterInfos.Length 
                    && arguments.Length >= parameterInfos.Length - m.ParameterDefaultValuesCount)
                {
                    if (methods.Length == 0 && arguments.Length == 0)
                    {
                        yield return new Tuple<MethodDescriptor, JsValue[]>(m, arguments);
                        yield break;
                    }

                    matchingByParameterCount ??= new List<Tuple<MethodDescriptor, JsValue[]>>();
                    matchingByParameterCount.Add(new Tuple<MethodDescriptor, JsValue[]>(m, arguments));
                }
            }

            if (matchingByParameterCount == null)
            {
                yield break;
            }

            foreach (var tuple in matchingByParameterCount)
            {
                var perfectMatch = true;
                var parameters = tuple.Item1.Parameters;
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
                    yield return new Tuple<MethodDescriptor, JsValue[]>(tuple.Item1, arguments);
                    yield break;
                }
            }

            for (var i = 0; i < matchingByParameterCount.Count; i++)
            {
                var tuple = matchingByParameterCount[i];
                yield return new Tuple<MethodDescriptor, JsValue[]>(tuple.Item1, tuple.Item2);
            }
        }

        internal static bool TypeIsNullable(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }
    }
}
