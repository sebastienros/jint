using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Runtime.CompilerServices;
using System.Threading;
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
    public class JsValue : IEquatable<JsValue>
    {
        // we can cache most common values, doubles are used in indexing too at times so we also cache
        // integer values converted to doubles
        private static readonly Dictionary<double, JsValue> _doubleToJsValue = new Dictionary<double, JsValue>();
        private static readonly JsValue[] _intToJsValue = new JsValue[1024];

        private const int AsciiMax = 126;
        private static readonly JsValue[] _charToJsValue = new JsValue[AsciiMax + 1];
        private static readonly JsValue[] _charToStringJsValue = new JsValue[AsciiMax + 1];

        private static readonly JsValue EmptyString = new JsValue("");
        private static readonly JsValue NullString = new JsValue("null");

        public static readonly JsValue Undefined = new JsValue(Types.Undefined);
        public static readonly JsValue Null = new JsValue(Types.Null);
        public static readonly JsValue False = new JsValue(false);
        public static readonly JsValue True = new JsValue(true);

        private readonly double _double;
        private readonly object _object;
        protected Types _type;

        static JsValue()
        {
            for (int i = 0; i < _intToJsValue.Length; i++)
            {
                _intToJsValue[i] = new JsValue(i);
                if (i != 0)
                {
                    // zero can be problematic
                    _doubleToJsValue[i] = new JsValue((double) i);
                }
            }
            for (int i = 0; i <= AsciiMax; i++)
            {
                _charToJsValue[i] = new JsValue((char) i);
                _charToStringJsValue[i] = new JsValue(((char) i).ToString());
            }
        }

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

        public JsValue(int value)
        {
            _object = null;
            _type = Types.Number;

            _double = value;
        }

        public JsValue(uint value)
        {
            _object = null;
            _type = Types.Number;

            _double = value;
        }

        public JsValue(char value)
        {
            _double = double.NaN;
            _object = value;
            _type = Types.String;
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

        public JsValue(Completion value)
        {
            _double = double.NaN;
            _type = Types.Completion;

            _object = value;
        }

        private JsValue(Types type)
        {
            _double = double.NaN;
            _object = null;
            _type = type;
        }

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
            return _type == Types.Object && _object is ArrayInstance;
        }

        [Pure]
        public bool IsDate()
        {
            return _type == Types.Object && _object is DateInstance;
        }

        [Pure]
        public bool IsRegExp()
        {
            return _type == Types.Object && _object is RegExpInstance;
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
        public bool IsCompletion()
        {
            return _type == Types.Completion;
        }

        [Pure]
        public bool IsSymbol()
        {
            return _type == Types.Symbol;
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
        public TInstance AsInstance<TInstance>() where TInstance : class
        {
            if (_type != Types.Object)
            {
                throw new ArgumentException("The value is not an object");
            }

            return _object as TInstance;
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
        public Completion AsCompletion()
        {
            if (_type != Types.Completion)
            {
                throw new ArgumentException("The value is not a completion record");
            }

            return (Completion)_object;
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

            fail?.Invoke(this);

            return null;
        }

        public bool Is<T>()
        {
            return _type == Types.Object && _object is T;
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

            return (string)_object;
        }

        [Pure]
        public string AsSymbol()
        {
            if (_type != Types.Symbol)
            {
                throw new ArgumentException("The value is not a symbol");
            }

            if (_object == null)
            {
                throw new ArgumentException("The value is not defined");
            }

            return (string)_object;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(JsValue other)
        {
            if (other == null)
            {
                return false;
            }

            if(ReferenceEquals(this, other))
            {
                return true;
            }

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

        public Types Type => _type;

        internal static JsValue FromInt(int value)
        {
            if (value >= 0 && value < _intToJsValue.Length)
            {
                return _intToJsValue[value];
            }
            return new JsValue(value);
        }

        internal static JsValue FromInt(uint value)
        {
            if (value >= 0 && value < _intToJsValue.Length)
            {
                return _intToJsValue[value];
            }
            return new JsValue(value);
        }

        internal static JsValue FromInt(ulong value)
        {
            if (value >= 0 && value < (ulong) _intToJsValue.Length)
            {
                return _intToJsValue[value];
            }
            return new JsValue(value);
        }

        internal static JsValue FromChar(char value)
        {
            if (value >= 0 && value <= AsciiMax)
            {
                return _charToJsValue[value];
            }
            return new JsValue(value);
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
                if (converter.TryConvert(value, out var result))
                {
                    return result;
                }
            }

            var valueType = value.GetType();

            var typeMappers = Engine.TypeMappers;

            if (typeMappers.TryGetValue(valueType, out var typeMapper))
            {
                return typeMapper(engine, value);
            }

            // if an ObjectInstance is passed directly, use it as is
            if (value is ObjectInstance instance)
            {
                // Learn conversion.
                // Learn conversion, racy, worst case we'll try again later
                Interlocked.CompareExchange(ref Engine.TypeMappers, new Dictionary<Type, Func<Engine, object, JsValue>>(typeMappers)
                {
                    [valueType] = (Engine e, object v) => ((ObjectInstance)v).JsValue
                }, typeMappers);
                return instance.JsValue;
            }

            var type = value as Type;
            if(type != null)
            {
                var typeReference = TypeReference.CreateTypeReference(engine, type);
                return typeReference.JsValue;
            }

            if (value is System.Array a)
            {
                JsValue Convert(Engine e, object v)
                {
                    var array = (System.Array) v;

                    var jsArray = engine.Array.Construct(a.Length);
                    foreach (var item in array)
                    {
                        var jsItem = FromObject(engine, item);
                        engine.Array.PrototypeObject.Push(jsArray, Arguments.From(jsItem));
                    }

                    return jsArray;
                }

                // racy, we don't care, worst case we'll catch up later
                Interlocked.CompareExchange(ref Engine.TypeMappers, new Dictionary<Type, Func<Engine, object, JsValue>>(typeMappers)
                {
                    [valueType] = Convert
                }, typeMappers);
                return Convert(engine, a);
            }

            if (value is Delegate d)
            {
                return new DelegateWrapper(engine, d);
            }

            if (value.GetType().IsEnum())
            {
                return FromInt((int) value);
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
                    if (_object is IObjectWrapper wrapper)
                    {
                        return wrapper.Target;
                    }

                    switch ((_object as ObjectInstance).Class)
                    {
                        case "Array":
                            if (_object is ArrayInstance arrayInstance)
                            {
                                var len = TypeConverter.ToInt32(arrayInstance.Get("length"));
                                var result = new object[len];
                                for (var k = 0; k < len; k++)
                                {
                                    var pk = TypeConverter.ToString(k);
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
                            if (_object is StringInstance stringInstance)
                            {
                                return stringInstance.PrimitiveValue.AsString();
                            }

                            break;

                        case "Date":
                            if (_object is DateInstance dateInstance)
                            {
                                return dateInstance.ToDateTime();
                            }

                            break;

                        case "Boolean":
                            if (_object is BooleanInstance booleanInstance)
                            {
                                return booleanInstance.PrimitiveValue.AsBoolean();
                            }

                            break;

                        case "Function":
                            if (_object is FunctionInstance function)
                            {
                                return (Func<JsValue, JsValue[], JsValue>)function.Call;
                            }

                            break;

                        case "Number":
                            if (_object is NumberInstance numberInstance)
                            {
                                return numberInstance.NumberData.AsNumber();
                            }

                            break;

                        case "RegExp":
                            if (_object is RegExpInstance regeExpInstance)
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

                            var objectInstance = (ObjectInstance) _object;
                            foreach (var p in objectInstance.GetOwnProperties())
                            {
                                if (!p.Value.Enumerable.HasValue || p.Value.Enumerable.Value == false)
                                {
                                    continue;
                                }

                                o.Add(p.Key, objectInstance.Get(p.Key).ToObject());
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

        public static bool ReturnOnAbruptCompletion(ref JsValue argument)
        {
            if (!argument.IsCompletion())
            {
                return false;
            }

            var completion = argument.AsCompletion();
            if (completion.IsAbrupt())
            {
                return true;
            }

            argument = completion.Value;

            return false;
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
            if ((object) a == null)
            {
                if ((object) b == null)
                {
                    return true;
                }

                return false;
            }

            if ((object) b == null)
            {
                return false;
            }

            return a.Equals(b);
        }

        public static bool operator !=(JsValue a, JsValue b)
        {
            if ((object) a == null)
            {
                if ((object) b == null)
                {
                    return false;
                }

                return true;
            }

            if ((object) b == null)
            {
                return true;
            }

            return !a.Equals(b);
        }

        static public implicit operator JsValue(char value)
        {
            return FromChar(value);
        }

        static public implicit operator JsValue(int value)
        {
            return FromInt(value);
        }

        static public implicit operator JsValue(uint value)
        {
            return FromInt(value);
        }

        static public implicit operator JsValue(double value)
        {
            if (value < 0 || value >= _doubleToJsValue.Count || !_doubleToJsValue.TryGetValue(value, out var jsValue))
            {
                jsValue = new JsValue(value);
            }
            return jsValue;
        }

        public static implicit operator JsValue(bool value)
        {
            return value ? True : False;
        }

        public static implicit operator JsValue(string value)
        {
            if (value.Length <= 1)
            {
                if (value == "")
                {
                    return EmptyString;
                }

                if (value.Length == 1)
                {
                    if (value[0] >= 0 && value[0] <= AsciiMax)
                    {
                        return _charToStringJsValue[value[0]];
                    }
                }

            }
            else if (value == Native.Null.Text)
            {
                return NullString;
            }

            return new JsValue(value);
        }

        public static implicit operator JsValue(ObjectInstance value)
        {
            return value.JsValue;
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
                    case Types.Symbol:
                        Value = value.AsSymbol() + " (symbol)";
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
            return obj is JsValue value && Equals(value);
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

    /// <summary>
    /// The _object value of a <see cref="JsSymbol"/> is the [[Description]] internal slot.
    /// </summary>
    public class JsSymbol : JsValue
    {
        public JsSymbol(string description) : base(description)
        {
            _type = Types.Symbol;
        }
    }
}