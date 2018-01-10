using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Threading;
using Jint.Native.Array;
using Jint.Native.Date;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native
{
    [DebuggerTypeProxy(typeof(JsValueDebugView))]
    public abstract class JsValue : IEquatable<JsValue>
    {
        public static readonly JsValue Undefined = new JsUndefined();
        public static readonly JsValue Null = new JsNull();

        [Pure]
        public bool IsPrimitive()
        {
            return Type != Types.Object && Type != Types.None;
        }

        [Pure]
        public bool IsUndefined()
        {
            return Type == Types.Undefined;
        }

        [Pure]
        public virtual bool IsArray()
        {
            return false;
        }

        [Pure]
        public virtual bool IsDate()
        {
            return false;
        }

        [Pure]
        public virtual bool IsRegExp()
        {
            return false;
        }

        [Pure]
        public bool IsObject()
        {
            return Type == Types.Object;
        }

        [Pure]
        public bool IsString()
        {
            return Type == Types.String;
        }

        [Pure]
        public bool IsNumber()
        {
            return Type == Types.Number;
        }

        [Pure]
        public bool IsBoolean()
        {
            return Type == Types.Boolean;
        }

        [Pure]
        public bool IsNull()
        {
            return Type == Types.Null;
        }

        [Pure]
        public bool IsCompletion()
        {
            return Type == Types.Completion;
        }

        [Pure]
        public bool IsSymbol()
        {
            return Type == Types.Symbol;
        }

        [Pure]
        public virtual ObjectInstance AsObject()
        {
            throw new ArgumentException("The value is not an object");
        }

        [Pure]
        public virtual TInstance AsInstance<TInstance>() where TInstance : class
        {
            throw new ArgumentException("The value is not an object");
        }

        [Pure]
        public virtual ArrayInstance AsArray()
        {
            throw new ArgumentException("The value is not an array");
        }

        [Pure]
        public virtual DateInstance AsDate()
        {
            throw new ArgumentException("The value is not a date");
        }

        [Pure]
        public virtual RegExpInstance AsRegExp()
        {
            throw new ArgumentException("The value is not a date");
        }

        [Pure]
        public virtual Completion AsCompletion()
        {
            if (Type != Types.Completion)
            {
                throw new ArgumentException("The value is not a completion record");
            }

            // TODO not implemented
            return null;
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

        public virtual bool Is<T>()
        {
            return false;
        }

        public virtual T As<T>() where T : ObjectInstance
        {
            return null;
        }

        [Pure]
        public virtual bool AsBoolean()
        {
            throw new ArgumentException("The value is not a boolean");
        }

        [Pure]
        public virtual string AsString()
        {
            throw new ArgumentException("The value is not a string");
        }

        [Pure]
        public virtual string AsSymbol()
        {
            throw new ArgumentException("The value is not a symbol");
        }

        [Pure]
        public virtual double AsNumber()
        {
            throw new ArgumentException("The value is not a number");
        }

        public abstract Types Type { get; }

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

            if (value is JsValue jsValue)
            {
                return jsValue;
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

            var type = value as Type;
            if (type != null)
            {
                var typeReference = TypeReference.CreateTypeReference(engine, type);
                return typeReference;
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
                return JsNumber.Create((int) value);
            }

            // if no known type could be guessed, wrap it as an ObjectInstance
            return new ObjectWrapper(engine, value);
        }

        /// <summary>
        /// Converts a <see cref="JsValue"/> to its underlying CLR value.
        /// </summary>
        /// <returns>The underlying CLR value of the <see cref="JsValue"/> instance.</returns>
        public abstract object ToObject();

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
            return "None";
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
            return JsString.Create(value);
        }

        static public implicit operator JsValue(int value)
        {
            return JsNumber.Create(value);
        }

        static public implicit operator JsValue(uint value)
        {
            return JsNumber.Create(value);
        }

        static public implicit operator JsValue(double value)
        {
            return JsNumber.Create(value);
        }

        public static implicit operator JsValue(bool value)
        {
            return value ? JsBoolean.True : JsBoolean.False;
        }

        public static implicit operator JsValue(string value)
        {
            return JsString.Create(value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is JsValue value && Equals(value);
        }

        public abstract bool Equals(JsValue other);

        public override int GetHashCode()
        {
            return Type.GetHashCode();
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
    }
}