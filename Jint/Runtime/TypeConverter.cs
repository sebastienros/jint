using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Jint.Native;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.String;

namespace Jint.Runtime
{
    public enum Types
    {
        None,
        Undefined,
        Null,
        Boolean,
        String,
        Number,
        Object
    }

    public class TypeConverter
    {
        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.1
        /// </summary>
        /// <param name="input"></param>
        /// <param name="preferredType"></param>
        /// <returns></returns>
        public static JsValue ToPrimitive(JsValue input, Types preferredType = Types.None)
        {
            if (input == Null.Instance || input == Undefined.Instance)
            {
                return input;
            }

            if (input.IsPrimitive())
            {
                return input;
            }

            return input.AsObject().DefaultValue(preferredType);
        }

    
        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.2
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static bool ToBoolean(JsValue o)
        {
            if (o.IsObject())
            {
                return true;
            }

            if (o == Undefined.Instance || o == Null.Instance)
            {
                return false;
            }
            
            if (o.IsBoolean())
            {
                return o.AsBoolean();
            }

            if (o.IsNumber())
            {
                var n = o.AsNumber();
                if (n.Equals(0) || double.IsNaN(n))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            if (o.IsString())
            {
                var s = o.AsString();
                if (String.IsNullOrEmpty(s))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }

            return true;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.3
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static double ToNumber(JsValue o)
        {
            // check number first as this is what is usually expected
            if (o.IsNumber())
            {
                return o.AsNumber();
            } 
            
            if (o.IsObject())
            {
                var p = o.AsObject() as IPrimitiveInstance;
                if (p != null)
                {
                    o = p.PrimitiveValue;
                }
            }

            if (o == Undefined.Instance)
            {
                return double.NaN;
            }

            if (o == Null.Instance)
            {
                return 0;
            }

            if (o.IsBoolean())
            {
                return o.AsBoolean() ? 1 : 0;
            }

            if (o.IsString())
            {
                var s = StringPrototype.TrimEx(o.AsString());

                if (String.IsNullOrEmpty(s))
                {
                    return 0;
                }

                if ("+Infinity".Equals(s) || "Infinity".Equals(s))
                {
                    return double.PositiveInfinity;
                }

                if ("-Infinity".Equals(s))
                {
                    return double.NegativeInfinity;
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

                        double n = Double.Parse(s,
                            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign |
                            NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite |
                            NumberStyles.AllowExponent, CultureInfo.InvariantCulture);
                        if (s.StartsWith("-") && n.Equals(0))
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

            return ToNumber(ToPrimitive(o, Types.Number));
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
            
            if (number.Equals(0) || double.IsInfinity(number))
            {
                return number;
            }

            return (long)number;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.5
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static int ToInt32(JsValue o)
        {
            return (int)(uint)ToNumber(o);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.6
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static uint ToUint32(JsValue o)
        {
            return (uint)ToNumber(o);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.7
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static ushort ToUint16(JsValue o)
        {
            return (ushort)(uint)ToNumber(o);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.8
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static string ToString(JsValue o)
        {
            if (o.IsObject())
            {
                var p = o.AsObject() as IPrimitiveInstance;
                if (p != null)
                {
                    o = p.PrimitiveValue;
                }
            }

            if (o.IsString())
            {
                return o.AsString();
            }

            if (o == Undefined.Instance)
            {
                return Undefined.Text;
            }

            if (o == Null.Instance)
            {
                return Null.Text;
            }
            
            if (o.IsBoolean())
            {
                return o.AsBoolean() ? "true" : "false";
            }

            if (o.IsNumber())
            {
                return NumberPrototype.ToNumberString(o.AsNumber());
            }

            return ToString(ToPrimitive(o, Types.String));
        }

        public static ObjectInstance ToObject(Engine engine, JsValue value)
        {
            if (value.IsObject())
            {
                return value.AsObject();
            }

            if (value == Undefined.Instance)
            {
                throw new JavaScriptException(engine.TypeError);
            }

            if (value == Null.Instance)
            {
                throw new JavaScriptException(engine.TypeError);
            }

            if (value.IsBoolean())
            {
                return engine.Boolean.Construct(value.AsBoolean());
            }

            if (value.IsNumber())
            {
                return engine.Number.Construct(value.AsNumber());
            }

            if (value.IsString())
            {
                return engine.String.Construct(value.AsString());
            }

            throw new JavaScriptException(engine.TypeError);
        }

        public static Types GetPrimitiveType(JsValue value)
        {
            if (value.IsObject())
            {
                var primitive = value.TryCast<IPrimitiveInstance>();
                if (primitive != null)
                {
                    return primitive.Type;
                }

                return Types.Object;
            }

            return value.Type;
        }

        public static void CheckObjectCoercible(Engine engine, JsValue o)
        {
            if (o == Undefined.Instance || o == Null.Instance)
            {
                throw new JavaScriptException(engine.TypeError);
            }
        }

        public static IEnumerable<MethodBase> FindBestMatch(Engine engine, MethodBase[] methods, JsValue[] arguments)
        {
            methods = methods
                .Where(m => m.GetParameters().Count() == arguments.Length)
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
