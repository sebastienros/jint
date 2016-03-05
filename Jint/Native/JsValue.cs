using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using Jint.Native.Array;
using Jint.Native.Boolean;
using Jint.Native.Date;
using Jint.Native.Function;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native
{
    [DebuggerTypeProxy(typeof(JsValueDebugView))]
    public struct JsValue : IEquatable<JsValue>
    {
        public readonly static JsValue Undefined = new JsValue(Types.Undefined);
        public readonly static JsValue Null = new JsValue(Types.Null);
        public readonly static JsValue False = new JsValue(false);
        public readonly static JsValue True = new JsValue(true);

        public JsValue(bool value)
        {
            _double = value ? 1.0 : 0.0;
            _object = null;
            _type = Types.Boolean;
        }

        public JsValue(double value)
        {
            _object = null;
            _type = Types.Number;

            _double = value;
        }

        public JsValue(string value)
        {
            _double = double.NaN;
            _object = value;
            _type = Types.String;
        }

        public JsValue(ObjectInstance value)
        {
            _double = double.NaN;
            _type = Types.Object;

            _object = value;
        }

        private JsValue(Types type)
        {
            _double = double.NaN;
            _object = null;
            _type = type;
        }

        private readonly double _double;

        private readonly object _object;

        private readonly Types _type;

        [Pure]
        public bool IsPrimitive()
        {
            return _type != Types.Object && _type != Types.None;
        }

        [Pure]
        public bool IsUndefined()
        {
            return _type == Types.Undefined;
        }

        [Pure]
        public bool IsArray()
        {
            return IsObject() && AsObject() is ArrayInstance;
        }

        [Pure]
        public bool IsDate()
        {
            return IsObject() && AsObject() is DateInstance;
        }

        [Pure]
        public bool IsRegExp()
        {
            return IsObject() && AsObject() is RegExpInstance;
        }

        [Pure]
        public bool IsObject()
        {
            return _type == Types.Object;
        }

        [Pure]
        public bool IsString()
        {
            return _type == Types.String;
        }

        [Pure]
        public bool IsNumber()
        {
            return _type == Types.Number;
        }

        [Pure]
        public bool IsBoolean()
        {
            return _type == Types.Boolean;
        }

        [Pure]
        public bool IsNull()
        {
            return _type == Types.Null;
        }

        [Pure]
        public ObjectInstance AsObject()
        {
            if (_type != Types.Object)
            {
                throw new ArgumentException("The value is not an object");
            }

            return _object as ObjectInstance;
        }

        [Pure]
        public ArrayInstance AsArray()
        {
            if (!IsArray())
            {
                throw new ArgumentException("The value is not an array");
            }

            return _object as ArrayInstance;
        }

        [Pure]
        public DateInstance AsDate()
        {
            if (!IsDate())
            {
                throw new ArgumentException("The value is not a date");
            }

            return _object as DateInstance;
        }

        [Pure]
        public RegExpInstance AsRegExp()
        {
            if (!IsRegExp())
            {
                throw new ArgumentException("The value is not a date");
            }

            return _object as RegExpInstance;
        }

        [Pure]
        public T TryCast<T>(Action<JsValue> fail = null) where T : class
        {
            if (IsObject())
            {
                var o = AsObject();
                var t = o as T;
                if (t != null)
                {
                    return t;
                }
            }

            if (fail != null)
            {
                fail(this);
            }

            return null;
        }

        public bool Is<T>()
        {
            return IsObject() && AsObject() is T;
        }

        public T As<T>() where T : ObjectInstance
        {
            return _object as T;
        }

        [Pure]
        public bool AsBoolean()
        {
            if (_type != Types.Boolean)
            {
                throw new ArgumentException("The value is not a boolean");
            }

            return _double != 0;
        }

        [Pure]
        public string AsString()
        {
            if (_type != Types.String)
            {
                throw new ArgumentException("The value is not a string");
            }

            if (_object == null)
            {
                throw new ArgumentException("The value is not defined");
            }

            return _object as string;
        }

        [Pure]
        public double AsNumber()
        {
            if (_type != Types.Number)
            {
                throw new ArgumentException("The value is not a number");
            }

            return _double;
        }

        public bool Equals(JsValue other)
        {
            if (_type != other._type)
            {
                return false;
            }

            switch (_type)
            {
                case Types.None:
                    return false;
                case Types.Undefined:
                    return true;
                case Types.Null:
                    return true;
                case Types.Boolean:
                case Types.Number:
                    return _double == other._double;
                case Types.String:
                case Types.Object:
                    return _object == other._object;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public Types Type
        {
            get { return _type; }
        }

        /// <summary>
        /// Creates a valid <see cref="JsValue"/> instance from any <see cref="Object"/> instance
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static JsValue FromObject(Engine engine, object value)
        {
            if (value == null)
            {
                return Null;
            }

            foreach (var converter in engine.Options._ObjectConverters)
            {
                JsValue result;
                if (converter.TryConvert(value, out result))
                {
                    return result;
                }
            }

            var typeCode = System.Type.GetTypeCode(value.GetType());
            switch (typeCode)
            {
                case TypeCode.Boolean:
                    return new JsValue((bool)value);
                case TypeCode.Byte:
                    return new JsValue((byte)value);
                case TypeCode.Char:
                    return new JsValue(value.ToString());
                case TypeCode.DateTime:
                    return engine.Date.Construct((DateTime)value);
                case TypeCode.Decimal:
                    return new JsValue((double)(decimal)value);
                case TypeCode.Double:
                    return new JsValue((double)value);
                case TypeCode.Int16:
                    return new JsValue((Int16)value);
                case TypeCode.Int32:
                    return new JsValue((Int32)value);
                case TypeCode.Int64:
                    return new JsValue((Int64)value);
                case TypeCode.SByte:
                    return new JsValue((SByte)value);
                case TypeCode.Single:
                    return new JsValue((Single)value);
                case TypeCode.String:
                    return new JsValue((string)value);
                case TypeCode.UInt16:
                    return new JsValue((UInt16)value);
                case TypeCode.UInt32:
                    return new JsValue((UInt32)value);
                case TypeCode.UInt64:
                    return new JsValue((UInt64)value);
                case TypeCode.Object:
                    break;
                case TypeCode.Empty:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (value is DateTimeOffset)
            {
                return engine.Date.Construct((DateTimeOffset)value);
            }

            // if an ObjectInstance is passed directly, use it as is
            var instance = value as ObjectInstance;
            if (instance != null)
            {
                return new JsValue(instance);
            }

            // if a JsValue is passed directly, use it as is
            if (value is JsValue)
            {
                return (JsValue)value;
            }

            var array = value as System.Array;
            if (array != null)
            {
                var jsArray = engine.Array.Construct(Arguments.Empty);
                foreach (var item in array)
                {
                    var jsItem = FromObject(engine, item);
                    engine.Array.PrototypeObject.Push(jsArray, Arguments.From(jsItem));
                }

                return jsArray;
            }

            var regex = value as System.Text.RegularExpressions.Regex;
            if (regex != null)
            {
                var jsRegex = engine.RegExp.Construct(regex.ToString().Trim('/'));
                return jsRegex;
            }

            var d = value as Delegate;
            if (d != null)
            {
                return new DelegateWrapper(engine, d);
            }

            if (value.GetType().IsEnum)
            {
                return new JsValue((Int32)value);
            }

            // if no known type could be guessed, wrap it as an ObjectInstance
            return new ObjectWrapper(engine, value);
        }

        /// <summary>
        /// Converts a <see cref="JsValue"/> to its underlying CLR value.
        /// </summary>
        /// <returns>The underlying CLR value of the <see cref="JsValue"/> instance.</returns>
        public object ToObject()
        {
            switch (_type)
            {
                case Types.None:
                case Types.Undefined:
                case Types.Null:
                    return null;
                case Types.String:
                    return _object;
                case Types.Boolean:
                    return _double != 0;
                case Types.Number:
                    return _double;
                case Types.Object:
                    var wrapper = _object as IObjectWrapper;
                    if (wrapper != null)
                    {
                        return wrapper.Target;
                    }

                    switch ((_object as ObjectInstance).Class)
                    {
                        case "Array":
                            var arrayInstance = _object as ArrayInstance;
                            if (arrayInstance != null)
                            {
                                var len = TypeConverter.ToInt32(arrayInstance.Get("length"));
                                var result = new object[len];
                                for (var k = 0; k < len; k++)
                                {
                                    var pk = k.ToString();
                                    var kpresent = arrayInstance.HasProperty(pk);
                                    if (kpresent)
                                    {
                                        var kvalue = arrayInstance.Get(pk);
                                        result[k] = kvalue.ToObject();
                                    }
                                    else
                                    {
                                        result[k] = null;
                                    }
                                }
                                return result;
                            }
                            break;

                        case "String":
                            var stringInstance = _object as StringInstance;
                            if (stringInstance != null)
                            {
                                return stringInstance.PrimitiveValue.AsString();
                            }

                            break;

                        case "Date":
                            var dateInstance = _object as DateInstance;
                            if (dateInstance != null)
                            {
                                return dateInstance.ToDateTime();
                            }

                            break;

                        case "Boolean":
                            var booleanInstance = _object as BooleanInstance;
                            if (booleanInstance != null)
                            {
                                return booleanInstance.PrimitiveValue.AsBoolean();
                            }

                            break;

                        case "Function":
                            var function = _object as FunctionInstance;
                            if (function != null)
                            {
                                return (Func<JsValue, JsValue[], JsValue>)function.Call;
                            }

                            break;

                        case "Number":
                            var numberInstance = _object as NumberInstance;
                            if (numberInstance != null)
                            {
                                return numberInstance.PrimitiveValue.AsNumber();
                            }

                            break;

                        case "RegExp":
                            var regeExpInstance = _object as RegExpInstance;
                            if (regeExpInstance != null)
                            {
                                return regeExpInstance.Value;
                            }

                            break;

                        case "Arguments":
                        case "Object":
#if __IOS__
                                IDictionary<string, object> o = new Dictionary<string, object>(); 
#else
                            IDictionary<string, object> o = new ExpandoObject();
#endif

                            foreach (var p in (_object as ObjectInstance).GetOwnProperties())
                            {
                                if (!p.Value.Enumerable.HasValue || p.Value.Enumerable.Value == false)
                                {
                                    continue;
                                }

                                o.Add(p.Key, (_object as ObjectInstance).Get(p.Key).ToObject());
                            }

                            return o;
                    }


                    return _object;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Invoke the current value as function.
        /// </summary>
        /// <param name="arguments">The arguments of the function call.</param>
        /// <returns>The value returned by the function call.</returns>
        public JsValue Invoke(params JsValue[] arguments)
        {
            return Invoke(Undefined, arguments);
        }

        /// <summary>
        /// Invoke the current value as function.
        /// </summary>
        /// <param name="thisObj">The this value inside the function call.</param>
        /// <param name="arguments">The arguments of the function call.</param>
        /// <returns>The value returned by the function call.</returns>
        public JsValue Invoke(JsValue thisObj, JsValue[] arguments)
        {
            var callable = TryCast<ICallable>();

            if (callable == null)
            {
                throw new ArgumentException("Can only invoke functions");
            }

            return callable.Call(thisObj, arguments);
        }

        public override string ToString()
        {
            switch (Type)
            {
                case Types.None:
                    return "None";
                case Types.Undefined:
                    return "undefined";
                case Types.Null:
                    return "null";
                case Types.Boolean:
                    return _double != 0 ? bool.TrueString : bool.FalseString;
                case Types.Number:
                    return _double.ToString();
                case Types.String:
                case Types.Object:
                    return _object.ToString();
                default:
                    return string.Empty;
            }
        }

        public static bool operator ==(JsValue a, JsValue b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(JsValue a, JsValue b)
        {
            return !a.Equals(b);
        }

        static public implicit operator JsValue(double value)
        {
            return new JsValue(value);
        }

        static public implicit operator JsValue(bool value)
        {
            return new JsValue(value);
        }

        static public implicit operator JsValue(string value)
        {
            return new JsValue(value);
        }

        static public implicit operator JsValue(ObjectInstance value)
        {
            return new JsValue(value);
        }

        internal class JsValueDebugView
        {
            public string Value;
            public JsValueDebugView(JsValue value)
            {

                switch (value.Type)
                {
                    case Types.None:
                        Value = "None";
                        break;
                    case Types.Undefined:
                        Value = "undefined";
                        break;
                    case Types.Null:
                        Value = "null";
                        break;
                    case Types.Boolean:
                        Value = value.AsBoolean() + " (bool)";
                        break;
                    case Types.String:
                        Value = value.AsString() + " (string)";
                        break;
                    case Types.Number:
                        Value = value.AsNumber() + " (number)";
                        break;
                    case Types.Object:
                        Value = value.AsObject().GetType().Name;
                        break;
                    default:
                        Value = "Unknown";
                        break;
                }
            }
        }
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is JsValue && Equals((JsValue)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 0;
                hashCode = (hashCode * 397) ^ _double.GetHashCode();
                hashCode = (hashCode * 397) ^ (_object != null ? _object.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)_type;
                return hashCode;
            }
        }
    }
}