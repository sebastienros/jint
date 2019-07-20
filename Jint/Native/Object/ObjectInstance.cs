using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native.Array;
using Jint.Native.Boolean;
using Jint.Native.Date;
using Jint.Native.Function;
using Jint.Native.Number;
using Jint.Native.RegExp;
using Jint.Native.String;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Native.Object
{
    public class ObjectInstance : JsValue, IEquatable<ObjectInstance>
    {
        private static readonly string ToPrimitiveSymbolName = GlobalSymbolRegistry.ToPrimitive._value;

        internal StringDictionarySlim<PropertyDescriptor> _properties;

        private bool _initialized;
        private readonly string _class;
        protected readonly Engine _engine;

        public ObjectInstance(Engine engine) : this(engine, "Object")
        {
        }

        protected ObjectInstance(Engine engine, string objectClass) : base(Types.Object)
        {
            _engine = engine;
            _class = objectClass;
        }

        public Engine Engine => _engine;

        /// <summary>
        /// The prototype of this object.
        /// </summary>
        public ObjectInstance Prototype { get; set; }

        /// <summary>
        /// If true, own properties may be added to the
        /// object.
        /// </summary>
        public bool Extensible { get; set; }

        /// <summary>
        /// A String value indicating a specification defined
        /// classification of objects.
        /// </summary>
        public string Class => _class;

        public virtual IEnumerable<KeyValuePair<string, PropertyDescriptor>> GetOwnProperties()
        {
            EnsureInitialized();

            if (_properties != null)
            {
                foreach (var pair in _properties)
                {
                    yield return pair;
                }
            }
        }

        protected virtual void AddProperty(in Key propertyName, PropertyDescriptor descriptor)
        {
            if (_properties == null)
            {
                _properties = new StringDictionarySlim<PropertyDescriptor>();
            }

            _properties[propertyName] = descriptor;
        }

        protected virtual bool TryGetProperty(in Key propertyName, out PropertyDescriptor descriptor)
        {
            if (_properties == null)
            {
                descriptor = null;
                return false;
            }

            return _properties.TryGetValue(propertyName, out descriptor);
        }

        public virtual bool HasOwnProperty(in Key propertyName)
        {
            EnsureInitialized();

            return _properties?.ContainsKey(propertyName) == true;
        }

        public virtual void RemoveOwnProperty(in Key propertyName)
        {
            EnsureInitialized();

            _properties?.Remove(propertyName);
        }

        /// <summary>
        /// Returns the value of the named property.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.3
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public virtual JsValue Get(in Key propertyName)
        {
            var desc = GetProperty(propertyName);
            return UnwrapJsValue(desc);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal JsValue UnwrapJsValue(PropertyDescriptor desc)
        {
            return UnwrapJsValue(desc, this);
        }

        internal static JsValue UnwrapJsValue(PropertyDescriptor desc, JsValue thisObject)
        {
            if (desc == PropertyDescriptor.Undefined)
            {
                return Undefined;
            }

            var value = (desc._flags & PropertyFlag.CustomJsValue) != 0
                ? desc.CustomValue
                : desc._value;

            // IsDataDescriptor inlined
            if ((desc._flags & (PropertyFlag.WritableSet | PropertyFlag.Writable)) != 0
                || !ReferenceEquals(value, null))
            {
                return value ?? Undefined;
            }

            var getter = desc.Get ?? Undefined;
            if (getter.IsUndefined())
            {
                return Undefined;
            }

            // if getter is not undefined it must be ICallable
            var callable = getter.TryCast<ICallable>();
            return callable.Call(thisObject, Arguments.Empty);
        }

        /// <summary>
        /// Returns the Property Descriptor of the named
        /// own property of this object, or undefined if
        /// absent.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.1
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public virtual PropertyDescriptor GetOwnProperty(in Key propertyName)
        {
            EnsureInitialized();

            PropertyDescriptor descriptor = null;
            _properties?.TryGetValue(propertyName, out descriptor);
            return descriptor ?? PropertyDescriptor.Undefined;
        }

        protected internal virtual void SetOwnProperty(in Key propertyName, PropertyDescriptor desc)
        {
            EnsureInitialized();

            if (_properties == null)
            {
                _properties = new StringDictionarySlim<PropertyDescriptor>();
            }

            _properties[propertyName] = desc;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.2
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PropertyDescriptor GetProperty(in Key propertyName)
        {
            var prop = GetOwnProperty(propertyName);

            if (prop != PropertyDescriptor.Undefined)
            {
                return prop;
            }

            return Prototype?.GetProperty(propertyName) ?? PropertyDescriptor.Undefined;
        }

        public bool TryGetValue(in Key propertyName, out JsValue value)
        {
            value = Undefined;
            var desc = GetOwnProperty(propertyName);
            if (desc != null && desc != PropertyDescriptor.Undefined)
            {
                if (desc == PropertyDescriptor.Undefined)
                {
                    return false;
                }

                var descValue = desc.Value;
                if (desc.WritableSet && !ReferenceEquals(descValue, null))
                {
                    value = descValue;
                    return true;
                }

                var getter = desc.Get ??  Undefined;
                if (getter.IsUndefined())
                {
                    value = Undefined;
                    return false;
                }

                // if getter is not undefined it must be ICallable
                var callable = getter.TryCast<ICallable>();
                value = callable.Call(this, Arguments.Empty);
                return true;
            }

            if (ReferenceEquals(Prototype, null))
            {
                return false;
            }

            return Prototype.TryGetValue(propertyName, out value);
        }

        /// <summary>
        /// Sets the specified named property to the value
        /// of the second parameter. The flag controls
        /// failure handling.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="value"></param>
        /// <param name="throwOnError"></param>
        public virtual void Put(in Key propertyName, JsValue value, bool throwOnError)
        {
            if (!CanPut(propertyName))
            {
                if (throwOnError)
                {
                    ExceptionHelper.ThrowTypeError(Engine);
                }

                return;
            }

            var ownDesc = GetOwnProperty(propertyName);

            if (ownDesc.IsDataDescriptor())
            {
                ownDesc.Value = value;
                return;

                // as per specification
                // var valueDesc = new PropertyDescriptor(value: value, writable: null, enumerable: null, configurable: null);
                // DefineOwnProperty(propertyName, valueDesc, throwOnError);
                // return;
            }

            // property is an accessor or inherited
            var desc = GetProperty(propertyName);

            if (desc.IsAccessorDescriptor())
            {
                var setter = desc.Set.TryCast<ICallable>();
                setter.Call(this, new[] {value});
            }
            else
            {
                var newDesc = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
                DefineOwnProperty(propertyName, newDesc, throwOnError);
            }
        }

        /// <summary>
        /// Returns a Boolean value indicating whether a
        /// [[Put]] operation with PropertyName can be
        /// performed.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.4
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public bool CanPut(in Key propertyName)
        {
            var desc = GetOwnProperty(propertyName);

            if (desc != PropertyDescriptor.Undefined)
            {
                if (desc.IsAccessorDescriptor())
                {
                    var set = desc.Set;
                    if (ReferenceEquals(set, null) || set.IsUndefined())
                    {
                        return false;
                    }

                    return true;
                }

                return desc.Writable;
            }

            if (ReferenceEquals(Prototype, null))
            {
                return Extensible;
            }

            var inherited = Prototype.GetProperty(propertyName);

            if (inherited == PropertyDescriptor.Undefined)
            {
                return Extensible;
            }

            if (inherited.IsAccessorDescriptor())
            {
                var set = inherited.Set;
                if (ReferenceEquals(set, null) || set.IsUndefined())
                {
                    return false;
                }

                return true;
            }

            if (!Extensible)
            {
                return false;
            }

            return inherited.Writable;
        }

        /// <summary>
        /// Returns a Boolean value indicating whether the
        /// object already has a property with the given
        /// name.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasProperty(in Key propertyName)
        {
            return GetProperty(propertyName) != PropertyDescriptor.Undefined;
        }

        /// <summary>
        /// Removes the specified named own property
        /// from the object. The flag controls failure
        /// handling.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="throwOnError"></param>
        /// <returns></returns>
        public virtual bool Delete(in Key propertyName, bool throwOnError)
        {
            var desc = GetOwnProperty(propertyName);

            if (desc == PropertyDescriptor.Undefined)
            {
                return true;
            }

            if (desc.Configurable)
            {
                RemoveOwnProperty(propertyName);
                return true;
            }

            if (throwOnError)
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }

            return false;
        }

        /// <summary>
        /// Hint is a String. Returns a default value for the object.
        /// </summary>
        public JsValue DefaultValue(Types hint)
        {
            EnsureInitialized();

            if (hint == Types.String || (hint == Types.None && Class == "Date"))
            {
                var jsValue = Get(ToPrimitiveSymbolName);
                if (!jsValue.IsNullOrUndefined())
                {
                    if (jsValue is ICallable toPrimitive)
                    {
                        var str = toPrimitive.Call(this, Arguments.Empty);
                        if (str.IsPrimitive())
                        {
                            return str;
                        }

                        if (str.IsObject())
                        {
                            return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "Cannot convert object to primitive value");
                        }
                    }

                    const string message = "'Value returned for property 'Symbol(Symbol.toPrimitive)' of object is not a function";
                    return ExceptionHelper.ThrowTypeError<JsValue>(_engine, message);
                }
                if (Get("toString") is ICallable toString)
                {
                    var str = toString.Call(this, Arguments.Empty);
                    if (str.IsPrimitive())
                    {
                        return str;
                    }
                }

                if (Get("valueOf") is ICallable valueOf)
                {
                    var val = valueOf.Call(this, Arguments.Empty);
                    if (val.IsPrimitive())
                    {
                        return val;
                    }
                }

                ExceptionHelper.ThrowTypeError(Engine);
            }

            if (hint == Types.Number || hint == Types.None)
            {
                var jsValue = Get(ToPrimitiveSymbolName);
                if (!jsValue.IsNullOrUndefined())
                {
                    if (jsValue is ICallable toPrimitive)
                    {
                        var val = toPrimitive.Call(this, Arguments.Empty);
                        if (val.IsPrimitive())
                        {
                            return val;
                        }

                        if (val.IsObject())
                        {
                            return ExceptionHelper.ThrowTypeError<JsValue>(_engine, "Cannot convert object to primitive value");
                        }
                    }

                    const string message = "'Value returned for property 'Symbol(Symbol.toPrimitive)' of object is not a function";
                    return ExceptionHelper.ThrowTypeError<JsValue>(_engine, message);
                }

                if (Get("valueOf") is ICallable valueOf)
                {
                    var val = valueOf.Call(this, Arguments.Empty);
                    if (val.IsPrimitive())
                    {
                        return val;
                    }
                }

                if (Get("toString") is ICallable toString)
                {
                    var str = toString.Call(this, Arguments.Empty);
                    if (str.IsPrimitive())
                    {
                        return str;
                    }
                }

                ExceptionHelper.ThrowTypeError(Engine);
            }

            return ToString();
        }

        /// <summary>
        /// Creates or alters the named own property to
        /// have the state described by a Property
        /// Descriptor. The flag controls failure handling.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="desc"></param>
        /// <param name="throwOnError"></param>
        /// <returns></returns>
        public virtual bool DefineOwnProperty(in Key propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            var current = GetOwnProperty(propertyName);

            if (current == desc)
            {
                return true;
            }

            var descValue = desc.Value;
            if (current == PropertyDescriptor.Undefined)
            {
                if (!Extensible)
                {
                    if (throwOnError)
                    {
                        ExceptionHelper.ThrowTypeError(Engine);
                    }

                    return false;
                }
                else
                {
                    if (desc.IsGenericDescriptor() || desc.IsDataDescriptor())
                    {
                        PropertyDescriptor propertyDescriptor;
                        if ((desc._flags & PropertyFlag.ConfigurableEnumerableWritable) == PropertyFlag.ConfigurableEnumerableWritable)
                        {
                            propertyDescriptor = new PropertyDescriptor(descValue ?? Undefined, PropertyFlag.ConfigurableEnumerableWritable);
                        }
                        else if ((desc._flags & PropertyFlag.ConfigurableEnumerableWritable) == 0)
                        {
                            propertyDescriptor = new PropertyDescriptor(descValue ?? Undefined, PropertyFlag.AllForbidden);
                        }
                        else
                        {
                            propertyDescriptor = new PropertyDescriptor(desc)
                            {
                                Value = descValue ?? Undefined
                            };
                        }

                        SetOwnProperty(propertyName, propertyDescriptor);
                    }
                    else
                    {
                        SetOwnProperty(propertyName, new GetSetPropertyDescriptor(desc));
                    }
                }

                return true;
            }

            // Step 5
            var currentGet = current.Get;
            var currentSet = current.Set;
            var currentValue = current.Value;

            if ((current._flags & PropertyFlag.ConfigurableSet | PropertyFlag.EnumerableSet | PropertyFlag.WritableSet) == 0 &&
                ReferenceEquals(currentGet, null) &&
                ReferenceEquals(currentSet, null) &&
                ReferenceEquals(currentValue, null))
            {
                return true;
            }

            // Step 6
            var descGet = desc.Get;
            var descSet = desc.Set;
            if (
                current.Configurable == desc.Configurable && current.ConfigurableSet == desc.ConfigurableSet &&
                current.Writable == desc.Writable && current.WritableSet == desc.WritableSet &&
                current.Enumerable == desc.Enumerable && current.EnumerableSet == desc.EnumerableSet &&
                ((ReferenceEquals(currentGet, null) && ReferenceEquals(descGet, null)) || (!ReferenceEquals(currentGet, null) && !ReferenceEquals(descGet, null) && JintExpression.SameValue(currentGet, descGet))) &&
                ((ReferenceEquals(currentSet, null) && ReferenceEquals(descSet, null)) || (!ReferenceEquals(currentSet, null) && !ReferenceEquals(descSet, null) && JintExpression.SameValue(currentSet, descSet))) &&
                ((ReferenceEquals(currentValue, null) && ReferenceEquals(descValue, null)) || (!ReferenceEquals(currentValue, null) && !ReferenceEquals(descValue, null) && JintBinaryExpression.StrictlyEqual(currentValue, descValue)))
            )
            {
                return true;
            }

            if (!current.Configurable)
            {
                if (desc.Configurable)
                {
                    if (throwOnError)
                    {
                        ExceptionHelper.ThrowTypeError(Engine);
                    }

                    return false;
                }

                if (desc.EnumerableSet && (desc.Enumerable != current.Enumerable))
                {
                    if (throwOnError)
                    {
                        ExceptionHelper.ThrowTypeError(Engine);
                    }

                    return false;
                }
            }

            if (!desc.IsGenericDescriptor())
            {
                if (current.IsDataDescriptor() != desc.IsDataDescriptor())
                {
                    if (!current.Configurable)
                    {
                        if (throwOnError)
                        {
                            ExceptionHelper.ThrowTypeError(Engine);
                        }

                        return false;
                    }

                    if (current.IsDataDescriptor())
                    {
                        var flags = current.Flags & ~(PropertyFlag.Writable | PropertyFlag.WritableSet);
                        SetOwnProperty(propertyName, current = new GetSetPropertyDescriptor(
                            get: JsValue.Undefined,
                            set: JsValue.Undefined,
                            flags
                        ));
                    }
                    else
                    {
                        var flags = current.Flags & ~(PropertyFlag.Writable | PropertyFlag.WritableSet);
                        SetOwnProperty(propertyName, current = new PropertyDescriptor(
                            value: JsValue.Undefined,
                            flags
                        ));
                    }
                }
                else if (current.IsDataDescriptor() && desc.IsDataDescriptor())
                {
                    if (!current.Configurable)
                    {
                        if (!current.Writable && desc.Writable)
                        {
                            if (throwOnError)
                            {
                                ExceptionHelper.ThrowTypeError(Engine);
                            }

                            return false;
                        }

                        if (!current.Writable)
                        {
                            if (!ReferenceEquals(descValue, null) && !JintExpression.SameValue(descValue, currentValue))
                            {
                                if (throwOnError)
                                {
                                    ExceptionHelper.ThrowTypeError(Engine);
                                }

                                return false;
                            }
                        }
                    }
                }
                else if (current.IsAccessorDescriptor() && desc.IsAccessorDescriptor())
                {
                    if (!current.Configurable)
                    {
                        if ((!ReferenceEquals(descSet, null) && !JintExpression.SameValue(descSet, currentSet ?? Undefined))
                            ||
                            (!ReferenceEquals(descGet, null) && !JintExpression.SameValue(descGet, currentGet ?? Undefined)))
                        {
                            if (throwOnError)
                            {
                                ExceptionHelper.ThrowTypeError(Engine);
                            }

                            return false;
                        }
                    }
                }
            }

            if (!ReferenceEquals(descValue, null))
            {
                current.Value = descValue;
            }

            if (desc.WritableSet)
            {
                current.Writable = desc.Writable;
            }

            if (desc.EnumerableSet)
            {
                current.Enumerable = desc.Enumerable;
            }

            if (desc.ConfigurableSet)
            {
                current.Configurable = desc.Configurable;
            }

            PropertyDescriptor mutable = null;
            if (!ReferenceEquals(descGet, null))
            {
                mutable = new GetSetPropertyDescriptor(mutable ?? current);
                ((GetSetPropertyDescriptor) mutable).SetGet(descGet);
            }

            if (!ReferenceEquals(descSet, null))
            {
                mutable = new GetSetPropertyDescriptor(mutable ?? current);
                ((GetSetPropertyDescriptor) mutable).SetSet(descSet);
            }

            if (mutable != null)
            {
                // replace old with new type that supports get and set
                FastSetProperty(propertyName, mutable);
            }

            return true;
        }

        /// <summary>
        /// Optimized version of [[Put]] when the property is known to be undeclared already
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        /// <param name="writable"></param>
        /// <param name="configurable"></param>
        /// <param name="enumerable"></param>
        public void FastAddProperty(string name, JsValue value, bool writable, bool enumerable, bool configurable)
        {
            SetOwnProperty(name, new PropertyDescriptor(value, writable, enumerable, configurable));
        }

        /// <summary>
        /// Optimized version of [[Put]] when the property is known to be already declared
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void FastSetProperty(in Key name, PropertyDescriptor value)
        {
            SetOwnProperty(name, value);
        }

        protected void EnsureInitialized()
        {
            if (!_initialized)
            {
                // we need to set flag eagerly to prevent wrong recursion
                _initialized = true;
                Initialize();
            }
        }

        protected virtual void Initialize()
        {
        }

        public override string ToString()
        {
            return TypeConverter.ToString(this);
        }

        public override object ToObject()
        {
            if (this is IObjectWrapper wrapper)
            {
                return wrapper.Target;
            }

            switch (Class)
            {
                case "Array":
                    if (this is ArrayInstance arrayInstance)
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
                    if (this is StringInstance stringInstance)
                    {
                        return stringInstance.PrimitiveValue.ToString();
                    }

                    break;

                case "Date":
                    if (this is DateInstance dateInstance)
                    {
                        return dateInstance.ToDateTime();
                    }

                    break;

                case "Boolean":
                    if (this is BooleanInstance booleanInstance)
                    {
                        return ((JsBoolean) booleanInstance.PrimitiveValue)._value
                             ? JsBoolean.BoxedTrue
                             : JsBoolean.BoxedFalse;
                    }

                    break;

                case "Function":
                    if (this is FunctionInstance function)
                    {
                        return (Func<JsValue, JsValue[], JsValue>) function.Call;
                    }

                    break;

                case "Number":
                    if (this is NumberInstance numberInstance)
                    {
                        return numberInstance.NumberData._value;
                    }

                    break;

                case "RegExp":
                    if (this is RegExpInstance regeExpInstance)
                    {
                        return regeExpInstance.Value;
                    }

                    break;

                case "Arguments":
                case "Object":
#if __IOS__
                                IDictionary<string, object> o = new DictionarySlim<string, object>();
#else
                    IDictionary<string, object> o = new ExpandoObject();
#endif

                    foreach (var p in GetOwnProperties())
                    {
                        if (!p.Value.Enumerable)
                        {
                            continue;
                        }

                        o.Add(p.Key, Get(p.Key).ToObject());
                    }

                    return o;
            }


            return this;
        }

        /// <summary>
        /// Handles the generic find of (callback[, thisArg])
        /// </summary>
        internal virtual bool FindWithCallback(
            JsValue[] arguments,
            out uint index,
            out JsValue value,
            bool visitUnassigned)
        {
            long GetLength()
            {
                var desc = GetProperty("length");
                var descValue = desc.Value;
                double len;
                if (desc.IsDataDescriptor() && !ReferenceEquals(descValue, null))
                {
                    len = TypeConverter.ToNumber(descValue);
                }
                else
                {
                    var getter = desc.Get ?? Undefined;
                    if (getter.IsUndefined())
                    {
                        len = 0;
                    }
                    else
                    {
                        // if getter is not undefined it must be ICallable
                        len = TypeConverter.ToNumber(((ICallable) getter).Call(this, Arguments.Empty));
                    }
                }

                return (long) System.Math.Max(
                    0, 
                    System.Math.Min(len, ArrayPrototype.ArrayOperations.MaxArrayLikeLength));
            }

            bool TryGetValue(uint idx, out JsValue jsValue)
            {
                var property = TypeConverter.ToString(idx);
                var kPresent = HasProperty(property);
                jsValue = kPresent ? Get(property) : Undefined;
                return kPresent;
            }

            if (GetLength() == 0)
            {
                index = 0;
                value = Undefined;
                return false;
            }
            
            var callbackfn = arguments.At(0);
            var thisArg = arguments.At(1);
            var callable = GetCallable(callbackfn);

            var args = _engine._jsValueArrayPool.RentArray(3);
            args[2] = this;
            var length = GetLength();
            for (uint k = 0; k < length; k++)
            {
                if (TryGetValue(k, out var kvalue) || visitUnassigned)
                {
                    args[0] = kvalue;
                    args[1] = k;
                    var testResult = callable.Call(thisArg, args);
                    if (TypeConverter.ToBoolean(testResult))
                    {
                        index = k;
                        value = kvalue;
                        return true;
                    }
                }
            }

            _engine._jsValueArrayPool.ReturnArray(args);

            index = 0;
            value = Undefined;
            return false;
        }

        protected ICallable GetCallable(JsValue source)
        {
            if (source is ICallable callable)
            {
                return callable;
            }

            ExceptionHelper.ThrowTypeError(_engine, "Argument must be callable");
            return null;
        }

        internal virtual bool IsConcatSpreadable => TryGetIsConcatSpreadable(out var isConcatSpreadable) && isConcatSpreadable;

        internal virtual bool IsArrayLike => TryGetValue("length", out var lengthValue)
                                             && lengthValue.IsNumber()
                                             && ((JsNumber) lengthValue)._value >= 0;

        protected bool TryGetIsConcatSpreadable(out bool isConcatSpreadable)
        {
            isConcatSpreadable = false;
            if (TryGetValue(GlobalSymbolRegistry.IsConcatSpreadable._value, out var isConcatSpreadableValue)
                && !ReferenceEquals(isConcatSpreadableValue, null)
                && !isConcatSpreadableValue.IsUndefined())
            {
                isConcatSpreadable = TypeConverter.ToBoolean(isConcatSpreadableValue);
                return true;
            }

            return false;
        }

        public override bool Equals(JsValue obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (!(obj is ObjectInstance s))
            {
                return false;
            }

            return Equals(s);
        }

        public bool Equals(ObjectInstance other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return false;
        }
    }
}