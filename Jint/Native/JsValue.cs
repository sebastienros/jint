using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Native.Array;
using Jint.Native.Date;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.RegExp;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native
{
    [DebuggerTypeProxy(typeof(JsValueDebugView))]
    public abstract class JsValue : IEquatable<JsValue>
    {
        public static readonly JsValue Undefined = new JsUndefined();
        public static readonly JsValue Null = new JsNull();
        internal readonly InternalTypes _type;

        protected JsValue(Types type)
        {
            _type = (InternalTypes) type;
        }

        internal JsValue(InternalTypes type)
        {
            _type = type;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPrimitive()
        {
            return _type != InternalTypes.Object && _type != InternalTypes.None;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsUndefined()
        {
            return _type == InternalTypes.Undefined;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsNullOrUndefined()
        {
            return _type < InternalTypes.Boolean;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsArray()
        {
            return this is ArrayInstance;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDate()
        {
            return this is DateInstance;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRegExp()
        {
            return this is RegExpInstance;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsObject()
        {
            return _type == InternalTypes.Object;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsString()
        {
            return _type == InternalTypes.String;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNumber()
        {
            return _type == InternalTypes.Number || _type == InternalTypes.Integer;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool IsInteger()
        {
            return _type == InternalTypes.Integer;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBoolean()
        {
            return _type == InternalTypes.Boolean;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNull()
        {
            return _type == InternalTypes.Null;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsCompletion()
        {
            return _type == InternalTypes.Completion;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSymbol()
        {
            return _type == InternalTypes.Symbol;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ObjectInstance AsObject()
        {
            if (!IsObject())
            {
                ExceptionHelper.ThrowArgumentException("The value is not an object");
            }
            return this as ObjectInstance;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TInstance AsInstance<TInstance>() where TInstance : class
        {
            if (!IsObject())
            {
                ExceptionHelper.ThrowArgumentException("The value is not an object");
            }
            return this as TInstance;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ArrayInstance AsArray()
        {
            if (!IsArray())
            {
                ExceptionHelper.ThrowArgumentException("The value is not an array");
            }
            return this as ArrayInstance;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IIterator GetIterator(Engine engine)
        {
            if (!TryGetIterator(engine, out var iterator))
            {
                return ExceptionHelper.ThrowTypeError<IIterator>(engine, "The value is not iterable");
            }

            return iterator;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetIterator(Engine engine, out IIterator iterator)
        {
            var objectInstance = TypeConverter.ToObject(engine, this);

            if (!objectInstance.TryGetValue(GlobalSymbolRegistry.Iterator._value, out var value)
                || !(value is ICallable callable))
            {
                iterator = null;
                return false;
            }

            var obj = callable.Call(this, Arguments.Empty) as ObjectInstance
                      ?? ExceptionHelper.ThrowTypeError<ObjectInstance>(engine, "Result of the Symbol.iterator method is not an object");

            if (obj is IIterator i)
            {
                iterator = i;
            }
            else
            {
                iterator = new IteratorInstance.ObjectWrapper(obj);
            }
            return true;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public DateInstance AsDate()
        {
            if (!IsDate())
            {
                ExceptionHelper.ThrowArgumentException("The value is not a date");
            }
            return this as DateInstance;
        }

        [Pure]
        public RegExpInstance AsRegExp()
        {
            if (!IsRegExp())
            {
                ExceptionHelper.ThrowArgumentException("The value is not a regex");
            }

            return this as RegExpInstance;
        }

        [Pure]
        public Completion AsCompletion()
        {
            if (_type != InternalTypes.Completion)
            {
                ExceptionHelper.ThrowArgumentException("The value is not a completion record");
            }

            // TODO not implemented
            return new Completion(CompletionType.Normal, Native.Undefined.Instance, null, default);
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T TryCast<T>() where T : class
        {
            return this as T;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T TryCast<T>(Action<JsValue> fail) where T : class
        {
            if (this is T o)
            {
                return o;
            }

            fail.Invoke(this);

            return null;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Is<T>()
        {
            return IsObject() && this is T;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T As<T>() where T : ObjectInstance
        {
            if (IsObject())
            {
                return this as T;
            }
            return null;
        }

        public Types Type
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _type == InternalTypes.Integer ? Types.Number : (Types) _type;
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

            if (value is JsValue jsValue)
            {
                return jsValue;
            }

            var converters = engine.Options._ObjectConverters;
            var convertersCount = converters.Count;
            for (var i = 0; i < convertersCount; i++)
            {
                var converter = converters[i];
                if (converter.TryConvert(engine, value, out var result))
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

            Type t = value.GetType();
            if (t.IsEnum)
            {
                Type ut = Enum.GetUnderlyingType(t);

                if (ut == typeof(ulong))
                    return JsNumber.Create(System.Convert.ToDouble(value));

                if (ut == typeof(uint) || ut == typeof(long))
                    return JsNumber.Create(System.Convert.ToInt64(value));

                return JsNumber.Create(System.Convert.ToInt32(value));
            }

            // if no known type could be guessed, wrap it as an ObjectInstance
            var h = engine.Options._WrapObjectHandler;
            ObjectInstance o = h != null ? h(value) : null;
            return o ?? new ObjectWrapper(engine, value);
        }

        private static JsValue Convert(Engine e, object v)
        {
            var array = (System.Array) v;
            var arrayLength = (uint) array.Length;

            var jsArray = new ArrayInstance(e, arrayLength);
            jsArray.Prototype = e.Array.PrototypeObject;
            jsArray.Extensible = true;

            for (uint i = 0; i < arrayLength; ++i)
            {
                var jsItem = FromObject(e, array.GetValue(i));
                jsArray.WriteArrayValue(i, new PropertyDescriptor(jsItem, PropertyFlag.ConfigurableEnumerableWritable));
            }

            jsArray.SetOwnProperty(KnownKeys.Length, new PropertyDescriptor(arrayLength, PropertyFlag.OnlyWritable));

            return jsArray;
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
            var callable = this as ICallable ?? ExceptionHelper.ThrowArgumentException<ICallable>("Can only invoke functions");
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
            if ((object)a == null)
            {
                if ((object)b == null)
                {
                    return true;
                }

                return false;
            }

            if ((object)b == null)
            {
                return false;
            }

            return a.Equals(b);
        }

        public static bool operator !=(JsValue a, JsValue b)
        {
            if ((object)a == null)
            {
                if ((object)b == null)
                {
                    return false;
                }

                return true;
            }

            if ((object)b == null)
            {
                return true;
            }

            return !a.Equals(b);
        }

        public static implicit operator JsValue(char value)
        {
            return JsString.Create(value);
        }

        public static implicit operator JsValue(int value)
        {
            return JsNumber.Create(value);
        }

        public static implicit operator JsValue(uint value)
        {
            return JsNumber.Create(value);
        }

        public static implicit operator JsValue(double value)
        {
            return JsNumber.Create(value);
        }

        public static implicit operator JsValue(long value)
        {
            return JsNumber.Create(value);
        }

        public static implicit operator JsValue(ulong value)
        {
            return JsNumber.Create(value);
        }

        public static implicit operator JsValue(bool value)
        {
            return value ? JsBoolean.True : JsBoolean.False;
        }

        public static implicit operator JsValue(string value)
        {
            if (value == null)
            {
                return Null;
            }

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
            return _type.GetHashCode();
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
                        Value = ((JsBoolean) value)._value + " (bool)";
                        break;
                    case Types.String:
                        Value = value.AsStringWithoutTypeCheck() + " (string)";
                        break;
                    case Types.Number:
                        Value = ((JsNumber) value)._value + " (number)";
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
