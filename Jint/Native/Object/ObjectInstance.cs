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
        internal StringDictionarySlim<PropertyDescriptor> _properties;

        private bool _initialized;
        internal ObjectInstance _prototype;
        private readonly string _class;
        protected readonly Engine _engine;

        public ObjectInstance(Engine engine) : this(engine, "Object")
        {
        }

        protected ObjectInstance(Engine engine, string objectClass) : base(Types.Object)
        {
            _engine = engine;
            _class = objectClass;
            Extensible = true;
        }

        public Engine Engine => _engine;

        /// <summary>
        /// The prototype of this object.
        /// </summary>
        public ObjectInstance Prototype => GetPrototypeOf();

        /// <summary>
        /// If true, own properties may be added to the
        /// object.
        /// </summary>
        public virtual bool Extensible { get; private set; }

        /// <summary>
        /// A String value indicating a specification defined
        /// classification of objects.
        /// </summary>
        public string Class => _class;

        public virtual IEnumerable<KeyValuePair<Key, PropertyDescriptor>> GetOwnProperties()
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
        
        internal virtual List<JsValue> GetOwnPropertyKeys(Types types)
        {
            EnsureInitialized();

            if (_properties == null)
            {
                return new List<JsValue>();
            }
            
            var keys = new List<JsValue>(_properties.Count);
            var propertyKeys = new List<JsValue>();
            List<JsValue> symbolKeys = null;
            
            foreach (var pair in _properties)
            {
                if ((pair.Key.Type & types) == 0)
                {
                    continue;
                }
                
                var isArrayIndex = ulong.TryParse(pair.Key, out var index);
                if (pair.Key.Type != Types.Symbol)
                {
                    if (isArrayIndex)
                    {
                        keys.Add(pair.Key);
                    }
                    else
                    {
                        propertyKeys.Add(pair.Key);
                    }
                }
                else
                {
                    symbolKeys ??= new List<JsValue>();
                    symbolKeys.Add(pair.Key);
                }
            }
            
            keys.Sort((v1, v2) => TypeConverter.ToNumber(v1).CompareTo(TypeConverter.ToNumber(v2)));

            keys.AddRange(propertyKeys);
            if (symbolKeys != null)
            {
                keys.AddRange(symbolKeys);
            }

            return keys;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JsValue Get(in Key propertyName)
        {
            return Get(propertyName, this);
        }

        /// <summary>
        /// Returns the value of the named property.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.3
        /// </summary>
        public virtual JsValue Get(in Key propertyName, JsValue receiver)
        {
            var desc = GetProperty(propertyName);
            return UnwrapJsValue(desc, receiver);
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Set(Key p, JsValue v, bool throwOnError)
        {
            if (!Set(p, v, this) && throwOnError)
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Set(in Key propertyName, JsValue value)
        {
            return Set(propertyName, value, this);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/#sec-ordinary-object-internal-methods-and-internal-slots-set-p-v-receiver
        /// </summary>
        public virtual bool Set(in Key propertyName, JsValue value, JsValue receiver)
        {
            var ownDesc = GetOwnProperty(propertyName);

            if (ownDesc == PropertyDescriptor.Undefined)
            {
                var parent = GetPrototypeOf();
                if (!(parent is null))
                {
                    return parent.Set(propertyName, value, receiver);
                }
                else
                {
                    ownDesc = new PropertyDescriptor(Undefined, PropertyFlag.ConfigurableEnumerableWritable);
                }
            }

            if (ownDesc.IsDataDescriptor())
            {
                if (!ownDesc.Writable)
                {
                    return false;
                }

                if (!(receiver is ObjectInstance oi))
                {
                    return false;
                }

                var existingDescriptor = oi.GetOwnProperty(propertyName);
                if (existingDescriptor != PropertyDescriptor.Undefined)
                {
                    if (existingDescriptor.IsAccessorDescriptor())
                    {
                        return false;
                    }

                    if (!existingDescriptor.Writable)
                    {
                        return false;
                    }

                    var valueDesc = new PropertyDescriptor(value, PropertyFlag.None);
                    return oi.DefineOwnProperty(propertyName, valueDesc);
                }
                else
                {
                    return oi.CreateDataProperty(propertyName, value);
                }
            }

            if (!(ownDesc.Set is ICallable setter))
            {
                return false;
            }

            setter.Call(receiver, new[] {value});

            return true;
        }
        
        /// <summary>
        /// Returns a Boolean value indicating whether a
        /// [[Put]] operation with PropertyName can be
        /// performed.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.4
        /// </summary>
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
        /// http://www.ecma-international.org/ecma-262/#sec-ordinary-object-internal-methods-and-internal-slots-hasproperty-p
        /// </summary>
        public virtual bool HasProperty(in Key propertyName)
        {
            var hasOwn = GetOwnProperty(propertyName);
            if (hasOwn != PropertyDescriptor.Undefined)
            {
                return true;
            }

            var parent = GetPrototypeOf();
            if (parent != null)
            {
                return parent.HasProperty(propertyName);
            }

            return false;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/#sec-deletepropertyorthrow
        /// </summary>
        public bool DeletePropertyOrThrow(in Key propertyName)
        {
            if (!Delete(propertyName))
            {
                ExceptionHelper.ThrowTypeError(Engine);
            }
            return true;
        }
        
        /// <summary>
        /// Removes the specified named own property
        /// from the object. The flag controls failure
        /// handling.
        /// </summary>
        public virtual bool Delete(in Key propertyName)
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
                var jsValue = Get(GlobalSymbolRegistry.ToPrimitive, this);
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
                if (Get("toString", this) is ICallable toString)
                {
                    var str = toString.Call(this, Arguments.Empty);
                    if (str.IsPrimitive())
                    {
                        return str;
                    }
                }

                if (Get("valueOf", this) is ICallable valueOf)
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
                var jsValue = Get(GlobalSymbolRegistry.ToPrimitive, this);
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

                if (Get("valueOf", this) is ICallable valueOf)
                {
                    var val = valueOf.Call(this, Arguments.Empty);
                    if (val.IsPrimitive())
                    {
                        return val;
                    }
                }

                if (Get("toString", this) is ICallable toString)
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

        public bool DefinePropertyOrThrow(in Key propertyName, PropertyDescriptor desc)
        {
            if (!DefineOwnProperty(propertyName, desc))
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            return true;
        } 
        
        /// <summary>
        /// Creates or alters the named own property to
        /// have the state described by a Property
        /// Descriptor. The flag controls failure handling.
        /// </summary>
        public virtual bool DefineOwnProperty(in Key propertyName, PropertyDescriptor desc)
        {
            var current = GetOwnProperty(propertyName);
            var extensible = Extensible;

            if (current == desc)
            {
                return true;
            }

            return ValidateAndApplyPropertyDescriptor(this, propertyName, extensible, desc, current);
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/#sec-validateandapplypropertydescriptor
        /// </summary>
        protected static bool ValidateAndApplyPropertyDescriptor(ObjectInstance o, in Key propertyName, bool extensible, PropertyDescriptor desc, PropertyDescriptor current)
        {
            var descValue = desc.Value;
            if (current == PropertyDescriptor.Undefined)
            {
                if (!extensible)
                {
                    return false;
                }

                if (o is object)
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

                        o.SetOwnProperty(propertyName, propertyDescriptor);
                    }
                    else
                    {
                        o.SetOwnProperty(propertyName, new GetSetPropertyDescriptor(desc));
                    }
                }

                return true;
            }

            // Step 3
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
                    return false;
                }

                if (desc.EnumerableSet && (desc.Enumerable != current.Enumerable))
                {
                    return false;
                }
            }

            if (!desc.IsGenericDescriptor())
            {
                if (current.IsDataDescriptor() != desc.IsDataDescriptor())
                {
                    if (!current.Configurable)
                    {
                        return false;
                    }


                    if (o is object)
                    {
                        var flags = current.Flags & ~(PropertyFlag.Writable | PropertyFlag.WritableSet);
                        if (current.IsDataDescriptor())
                        {
                            o.SetOwnProperty(propertyName, current = new GetSetPropertyDescriptor(
                                get: Undefined,
                                set: Undefined,
                                flags
                            ));
                        }
                        else
                        {
                            o.SetOwnProperty(propertyName, current = new PropertyDescriptor(
                                value: Undefined,
                                flags
                            ));
                        }
                    }
                }
                else if (current.IsDataDescriptor() && desc.IsDataDescriptor())
                {
                    if (!current.Configurable)
                    {
                        if (!current.Writable && desc.Writable)
                        {
                            return false;
                        }

                        if (!current.Writable)
                        {
                            if (!ReferenceEquals(descValue, null) && !JintExpression.SameValue(descValue, currentValue))
                            {
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
                            return false;
                        }
                    }
                }
            }

            if (o is object)
            {
                
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
                    o.FastSetProperty(propertyName, mutable);
                }
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
                        var len = TypeConverter.ToInt32(arrayInstance.Get("length", arrayInstance));
                        var result = new object[len];
                        for (var k = 0; k < len; k++)
                        {
                            var pk = TypeConverter.ToString(k);
                            var kpresent = arrayInstance.HasProperty(pk);
                            if (kpresent)
                            {
                                var kvalue = arrayInstance.Get(pk, arrayInstance);
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

                        o.Add(p.Key, Get(p.Key, this).ToObject());
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
                    System.Math.Min(len, ArrayOperations.MaxArrayLikeLength));
            }

            bool TryGetValue(uint idx, out JsValue jsValue)
            {
                var property = TypeConverter.ToString(idx);
                var kPresent = HasProperty(property);
                jsValue = kPresent ? Get(property, this) : Undefined;
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

        internal bool IsConcatSpreadable
        {
            get
            {
                var spreadable = Get(GlobalSymbolRegistry.IsConcatSpreadable, this);
                if (!spreadable.IsUndefined())
                {
                    return TypeConverter.ToBoolean(spreadable);
                }
                return IsArray();
            }
        }

        internal virtual bool IsArrayLike => TryGetValue("length", out var lengthValue)
                                             && lengthValue.IsNumber()
                                             && ((JsNumber) lengthValue)._value >= 0;

        public virtual JsValue PreventExtensions()
        {
            Extensible = false;
            return JsBoolean.True;
        }

        protected virtual ObjectInstance GetPrototypeOf()
        {
            return _prototype;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ordinarysetprototypeof
        /// </summary>
        public virtual bool SetPrototypeOf(JsValue value)
        {
            if (!value.IsObject() && !value.IsNull())
            {
                ExceptionHelper.ThrowArgumentException();
            }

            var current = _prototype ?? Null;
            if (ReferenceEquals(value, current))
            {
                return true;
            }

            if (!Extensible)
            {
                return false;
            }

            if (value.IsNull())
            {
                _prototype = null;
                return true;
            }

            // validate chain
            var p = value as ObjectInstance;
            bool done = false;
            while (!done)
            {
                if (p is null)
                {
                    done = true;
                }
                else if (ReferenceEquals(p, this))
                {
                    return false;
                }
                else
                {
                    p = p._prototype;
                }
            }

            _prototype = value as ObjectInstance;
            return true;
        }
        
        /// <summary>
        /// http://www.ecma-international.org/ecma-262/#sec-createdatapropertyorthrow
        /// </summary>
        internal bool CreateDataProperty(Key p, JsValue v)
        {
            var newDesc = new PropertyDescriptor(v, PropertyFlag.ConfigurableEnumerableWritable);
            return DefineOwnProperty(p, newDesc);
        }   
        
        /// <summary>
        /// http://www.ecma-international.org/ecma-262/#sec-createdataproperty
        /// </summary>
        internal bool CreateDataPropertyOrThrow( Key p, JsValue v)
        {
            if (!CreateDataProperty(p, v))
            {
                ExceptionHelper.ThrowTypeError(_engine);
            }

            return true;
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