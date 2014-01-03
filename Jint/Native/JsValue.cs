using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native
{
    [DebuggerTypeProxy(typeof(JsValueDebugView))]
    public struct JsValue : IEquatable<JsValue>
    {
        public static JsValue Undefined = new JsValue(Types.Undefined);
        public static JsValue Null = new JsValue(Types.Null);
        public static JsValue False = new JsValue(false);
        public static JsValue True = new JsValue(true);

        public JsValue(bool value)
        {
            _bool = value;
            _double = null;
            _object = null;
            _string = null;
            _type = Types.Boolean;
        }

        public JsValue(double value)
        {
            _bool = null;
            _double = value;
            _object = null;
            _string = null;
            _type = Types.Number;
        }

        public JsValue(string value)
        {
            _bool = null;
            _double = null;
            _object = null;
            _string = value;
            _type = Types.String;
        }

        public JsValue(ObjectInstance value)
        {
            _bool = null;
            _double = null;
            _object = value;
            _string = null;
            _type = Types.Object;
        }

        private JsValue(Types type)
        {
            _bool = null;
            _double = null;
            _object = null;
            _string = null;
            _type = type;
        }

        private readonly bool? _bool;

        private readonly double? _double;

        private readonly ObjectInstance _object;

        private readonly string _string;

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

            return _object;
        }

        [Pure]
        public T TryCast<T>(Action<JsValue> fail = null) where T: class
        {
            if (this.IsObject())
            {
                var o = this.AsObject();
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

            if (!_bool.HasValue)
            {
                throw new ArgumentException("The value is not defined");
            }

            return _bool.Value;
        }

        [Pure]
        public string AsString()
        {
            if (_type != Types.String)
            {
                throw new ArgumentException("The value is not a string");
            }

            if (_string == null)
            {
                throw new ArgumentException("The value is not defined");
            }

            return _string;
        }

        [Pure]
        public double AsNumber()
        {
            if (_type != Types.Number)
            {
                throw new ArgumentException("The value is not a number");
            }

            if (!_double.HasValue)
            {
                throw new ArgumentException("The value is not defined");
            }

            return _double.Value;
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
                    return _bool == other._bool;
                case Types.String:
                    return _string == other._string;
                case Types.Number:
                    return _double == other._double;
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

        private static readonly Type[] NumberTypes = { typeof(double), typeof(int), typeof(float), typeof(uint), typeof(byte), typeof(short), typeof(ushort), typeof(long), typeof(ulong) };
        public static JsValue FromObject(object value)
        {
            if (value == null)
            {
                return Null;
            }

            var s = value as string;
            if (s != null)
            {
                return s;
            }

            if (System.Array.IndexOf(NumberTypes, value.GetType()) != -1)
            {
                return Convert.ToDouble(value);
            }

            if (value is bool)
            {
                return (bool) value;
            }

            return Undefined;
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
                case Types.Boolean:
                    return _bool;
                case Types.String:
                    return _string;
                case Types.Number:
                    return _double;
                case Types.Object:
                    return _object;
                default:
                    throw new ArgumentOutOfRangeException();
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
    }

    public static class Undefined
    {
        public static JsValue Instance = JsValue.Undefined;
        public static string Text = "undefined";
    }

    public static class Null
    {
        public static JsValue Instance = JsValue.Null;
        public static string Text = "null";
    }
}
