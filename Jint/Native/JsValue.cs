using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Jint.Native.Array;
using Jint.Native.Date;
using Jint.Native.Iterator;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Promise;
using Jint.Native.RegExp;
using Jint.Native.Symbol;
using Jint.Runtime;

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
            return (_type & (InternalTypes.Primitive | InternalTypes.Undefined | InternalTypes.Null)) != 0;
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
        public virtual bool IsArray() => false;

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsDate()
        {
            return this is DateInstance;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsPromise()
        {
            return this is PromiseInstance;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsRegExp()
        {
            if (!(this is ObjectInstance oi))
            {
                return false;
            }

            var matcher = oi.Get(GlobalSymbolRegistry.Match);
            if (!matcher.IsUndefined())
            {
                return TypeConverter.ToBoolean(matcher);
            }

            return this is RegExpInstance;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsObject()
        {
            return (_type & InternalTypes.Object) != 0;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsString()
        {
            return (_type & InternalTypes.String) != 0;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsNumber()
        {
            return (_type & (InternalTypes.Number | InternalTypes.Integer)) != 0;
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

        internal virtual bool IsIntegerIndexedArray => false;

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
        internal IIterator GetIterator(Realm realm)
        {
            if (!TryGetIterator(realm, out var iterator))
            {
                ExceptionHelper.ThrowTypeError(realm, "The value is not iterable");
            }

            return iterator;
        }

        [Pure]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetIterator(Realm realm, out IIterator iterator)
        {
            var objectInstance = TypeConverter.ToObject(realm, this);

            if (!objectInstance.TryGetValue(GlobalSymbolRegistry.Iterator, out var value)
                || !(value is ICallable callable))
            {
                iterator = null;
                return false;
            }

            var obj = callable.Call(this, Arguments.Empty) as ObjectInstance;
            if (obj is null)
            {
                ExceptionHelper.ThrowTypeError(realm, "Result of the Symbol.iterator method is not an object");
            }

            if (obj is IIterator i)
            {
                iterator = i;
            }
            else
            {
                iterator = new IteratorInstance.ObjectIterator(obj);
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
            get => _type == InternalTypes.Integer
                ? Types.Number
                : (Types) (_type & ~InternalTypes.InternalFlags);
        }

        internal virtual bool IsConstructor => false;

        /// <summary>
        /// Creates a valid <see cref="JsValue"/> instance from any <see cref="Object"/> instance
        /// </summary>
        public static JsValue FromObject(Engine engine, object value)
        {
            if (value is null)
            {
                return Null;
            }

            if (value is JsValue jsValue)
            {
                return jsValue;
            }

            if (engine._objectConverters != null)
            {
                foreach (var converter in engine._objectConverters)
                {
                    if (converter.TryConvert(engine, value, out var result))
                    {
                        return result;
                    }
                }
            }

            if (DefaultObjectConverter.TryConvert(engine, value, out var defaultConversion))
            {
                return defaultConversion;
            }

            return null;
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
            var callable = this as ICallable;
            if (callable is null)
            {
                ExceptionHelper.ThrowTypeErrorNoEngine("Can only invoke functions");
            }
            return callable.Call(Undefined, arguments);
        }

        public virtual bool HasOwnProperty(JsValue property) => false;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsValue Get(JsValue property)
        {
            return Get(property, this);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.3
        /// </summary>
        public virtual JsValue Get(JsValue property, JsValue receiver)
        {
            return Undefined;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ordinary-object-internal-methods-and-internal-slots-set-p-v-receiver
        /// </summary>
        public virtual bool Set(JsValue property, JsValue value, JsValue receiver)
        {
            ExceptionHelper.ThrowNotSupportedException();
            return false;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-instanceofoperator
        /// </summary>
        internal bool InstanceofOperator(JsValue target)
        {
            var oi = target as ObjectInstance;
            if (oi is null)
            {
                ExceptionHelper.ThrowTypeErrorNoEngine("not an object");
            }

            var instOfHandler = oi.GetMethod(GlobalSymbolRegistry.HasInstance);
            if (instOfHandler is not null)
            {
                return TypeConverter.ToBoolean(instOfHandler.Call(target, new[] {this}));
            }

            if (!target.IsCallable)
            {
                ExceptionHelper.ThrowTypeErrorNoEngine("not callable");
            }

            return target.OrdinaryHasInstance(this);
        }

        public override string ToString()
        {
            return "None";
        }

        public static bool operator ==(JsValue a, JsValue b)
        {
            if ((object) a == null)
            {
                return (object) b == null;
            }

            return (object) b != null && a.Equals(b);
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

        [DebuggerStepThrough]
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
                        Value = value.ToString() + " (string)";
                        break;
                    case Types.Number:
                        Value = ((JsNumber) value)._value + " (number)";
                        break;
                    case Types.Object:
                        Value = value.AsObject().GetType().Name;
                        break;
                    case Types.Symbol:
                        var jsValue = ((JsSymbol) value)._value;
                        Value = (jsValue.IsUndefined() ? "" : jsValue.ToString()) + " (symbol)";
                        break;
                    default:
                        Value = "Unknown";
                        break;
                }
            }
        }

        /// <summary>
        /// Some values need to be cloned in order to be assigned, like ConcatenatedString.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal JsValue Clone()
        {
            // concatenated string and arguments currently may require cloning
            return (_type & InternalTypes.RequiresCloning) == 0
                ? this
                : DoClone();
        }

        internal virtual JsValue DoClone()
        {
            return this;
        }

        internal virtual bool IsCallable => this is ICallable;

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ordinaryhasinstance
        /// </summary>
        internal virtual bool OrdinaryHasInstance(JsValue v)
        {
            if (!IsCallable)
            {
                return false;
            }

            if (v is not ObjectInstance o)
            {
                return false;
            }

            var p = Get(CommonProperties.Prototype);
            if (p is not ObjectInstance)
            {
                ExceptionHelper.ThrowTypeError(o.Engine.Realm, $"Function has non-object prototype '{TypeConverter.ToString(p)}' in instanceof check");
            }

            while (true)
            {
                o = o.Prototype;

                if (o is null)
                {
                    return false;
                }

                if (SameValue(p, o))
                {
                    return true;
                }
            }
        }

        internal static bool SameValue(JsValue x, JsValue y)
        {
            var typea = x.Type;
            var typeb = y.Type;

            if (typea != typeb)
            {
                return false;
            }

            switch (typea)
            {
                case Types.Number:
                    if (x._type == y._type && x._type == InternalTypes.Integer)
                    {
                        return x.AsInteger() == y.AsInteger();
                    }

                    var nx = TypeConverter.ToNumber(x);
                    var ny = TypeConverter.ToNumber(y);

                    if (double.IsNaN(nx) && double.IsNaN(ny))
                    {
                        return true;
                    }

                    if (nx == ny)
                    {
                        if (nx == 0)
                        {
                            // +0 !== -0
                            return NumberInstance.IsNegativeZero(nx) == NumberInstance.IsNegativeZero(ny);
                        }

                        return true;
                    }

                    return false;
                case Types.String:
                    return TypeConverter.ToString(x) == TypeConverter.ToString(y);
                case Types.Boolean:
                    return TypeConverter.ToBoolean(x) == TypeConverter.ToBoolean(y);
                case Types.Undefined:
                case Types.Null:
                    return true;
                case Types.Symbol:
                    return x == y;
                default:
                    return ReferenceEquals(x, y);
            }
        }

        internal static IConstructor AssertConstructor(Engine engine, JsValue c)
        {
            var constructor = c as IConstructor;
            if (constructor is null)
            {
                ExceptionHelper.ThrowTypeError(engine.Realm, c + " is not a constructor");
            }

            return constructor;
        }
    }
}