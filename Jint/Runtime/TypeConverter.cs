using System;
using System.Globalization;
using Jint.Native;
using Jint.Native.Number;

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
        public static JsValue ToBoolean(JsValue o)
        {
            if (o.IsObject())
            {
                var p = o.AsObject() as IPrimitiveInstance;
                if (p != null)
                {
                    o = p.PrimitiveValue;
                }
            }

            if (o == Undefined.Instance || o == Null.Instance)
            {
                return JsValue.False;
            }
            
            if (o.IsBoolean())
            {
                return o;
            }

            if (o.IsNumber())
            {
                var n = o.AsNumber();
                if (n == 0 || double.IsNaN(n))
                {
                    return JsValue.False;
                }
                else
                {
                    return JsValue.True;
                }
            }

            if (o.IsString())
            {
                var s = o.AsString();
                if (String.IsNullOrEmpty(s))
                {
                    return JsValue.False;
                }
                else
                {
                    return JsValue.True;
                }
            }
            
            return JsValue.True;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.3
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static JsValue ToNumber(JsValue o)
        {
            if (o.IsObject())
            {
                var p = o.AsObject() as IPrimitiveInstance;
                if (p != null)
                {
                    o = p.PrimitiveValue;
                }
            }

            if (o.IsNumber())
            {
                return o;
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
                double n;
                var s = o.AsString().Trim();

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

                        n = Double.Parse(s,
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

                return double.NaN;
            }

            return ToNumber(ToPrimitive(o, Types.Number));
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.4
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static JsValue ToInteger(JsValue o)
        {
            var number = ToNumber(o).AsNumber();

            if (double.IsNaN(number))
            {
                return 0;
            }
            
            if (number == 0 || number == double.NegativeInfinity || number == double.PositiveInfinity)
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
            return (int)(uint)ToNumber(o).AsNumber();
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.6
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static uint ToUint32(JsValue o)
        {
            return (uint)ToNumber(o).AsNumber();
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.7
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static ushort ToUint16(JsValue o)
        {
            return (ushort)(uint)ToNumber(o).AsNumber();
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-9.8
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static JsValue ToString(JsValue o)
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
                return o;
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

        public static JsValue ToObject(Engine engine, JsValue value)
        {
            if (value.IsObject())
            {
                return value;
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

        public static void CheckObjectCoercible(Engine engine, JsValue o)
        {
            if (o == Undefined.Instance || o == Null.Instance)
            {
                throw new JavaScriptException(engine.TypeError);
            }
        }

    }
}
