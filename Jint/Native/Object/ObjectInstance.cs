using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using Jint.Native.Array;
using Jint.Native.Boolean;
using Jint.Native.Date;
using Jint.Native.Function;
using Jint.Native.Number;
using Jint.Native.RegExp;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interop;

namespace Jint.Native.Object
{
    public class ObjectInstance : JsValue, IEquatable<ObjectInstance>
    {
        private const string PropertyNamePrototype = "prototype";
        private const string PropertyNameConstructor = "constructor";
        private const string PropertyNameLength = "length";

        private Dictionary<string, IPropertyDescriptor> _intrinsicProperties;
        private MruPropertyCache2<string, IPropertyDescriptor> _properties;

        private IPropertyDescriptor _prototype;
        private IPropertyDescriptor _constructor;
        private IPropertyDescriptor _length;

        public ObjectInstance(Engine engine)
        {
            Engine = engine;
        }

        public Engine Engine { get; set; }

        protected bool TryGetIntrinsicValue(JsSymbol symbol, out JsValue value)
        {
            IPropertyDescriptor descriptor;

            if (_intrinsicProperties != null && _intrinsicProperties.TryGetValue(symbol.AsSymbol(), out descriptor))
            {
                value = descriptor.Value;
                return true;
            }

            if (Prototype == null)
            {
                value = Undefined;
                return false;
            }

            return Prototype.TryGetIntrinsicValue(symbol, out value);
        }

        public void SetIntrinsicValue(string name, JsValue value, bool writable, bool enumerable, bool configurable)
        {
            SetOwnProperty(name, new PropertyDescriptor(value, writable, enumerable, configurable));
        }

        protected void SetIntrinsicValue(JsSymbol symbol, JsValue value, bool writable, bool enumerable, bool configurable)
        {
            if (_intrinsicProperties == null)
            {
                _intrinsicProperties = new Dictionary<string, IPropertyDescriptor>();
            }

            _intrinsicProperties[symbol.AsSymbol()] = new PropertyDescriptor(value, writable, enumerable, configurable);
        }

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
        public virtual string Class => "Object";

        public virtual IEnumerable<KeyValuePair<string, IPropertyDescriptor>> GetOwnProperties()
        {
            EnsureInitialized();

            if (_prototype != null)
            {
                yield return new KeyValuePair<string, IPropertyDescriptor>(PropertyNamePrototype, _prototype);
            }

            if (_constructor != null)
            {
                yield return new KeyValuePair<string, IPropertyDescriptor>(PropertyNameConstructor, _constructor);
            }

            if (_length != null)
            {
                yield return new KeyValuePair<string, IPropertyDescriptor>(PropertyNameLength, _length);
            }

            if (_properties != null)
            {
                foreach (var pair in _properties.GetEnumerator())
                {
                    yield return pair;
                }
            }
        }

        protected void AddProperty(string propertyName, IPropertyDescriptor descriptor)
        {
            if (propertyName == PropertyNamePrototype)
            {
                _prototype = descriptor;
                return;
            }

            if (propertyName == PropertyNameConstructor)
            {
                _constructor = descriptor;
                return;
            }

            if (propertyName == PropertyNameLength)
            {
                _length = descriptor;
                return;
            }

            if (_properties == null)
            {
                _properties = new MruPropertyCache2<string, IPropertyDescriptor>();
            }

            _properties.Add(propertyName, descriptor);
        }

        protected bool TryGetProperty(string propertyName, out IPropertyDescriptor descriptor)
        {
            if (propertyName == PropertyNamePrototype)
            {
                descriptor = _prototype;
                return _prototype != null;
            }

            if (propertyName == PropertyNameConstructor)
            {
                descriptor = _constructor;
                return _constructor != null;
            }

            if (propertyName == PropertyNameLength)
            {
                descriptor = _length;
                return _length != null;
            }

            if (_properties == null)
            {
                descriptor = null;
                return false;
            }

            return _properties.TryGetValue(propertyName, out descriptor);
        }

        public virtual bool HasOwnProperty(string propertyName)
        {
            EnsureInitialized();

            if (propertyName == PropertyNamePrototype)
            {
                return _prototype != null;
            }

            if (propertyName == PropertyNameConstructor)
            {
                return _constructor != null;
            }

            if (propertyName == PropertyNameLength)
            {
                return _length != null;
            }

            return _properties?.ContainsKey(propertyName) ?? false;
        }

        public virtual void RemoveOwnProperty(string propertyName)
        {
            EnsureInitialized();

            if (propertyName == PropertyNamePrototype)
            {
                _prototype = null;
            }

            if (propertyName == PropertyNameConstructor)
            {
                _constructor = null;
            }

            if (propertyName == PropertyNameLength)
            {
                _length = null;
            }

            _properties?.Remove(propertyName);
        }

        /// <summary>
        /// Returns the value of the named property.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.3
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public virtual JsValue Get(string propertyName)
        {
            var desc = GetProperty(propertyName);

            if (desc == PropertyDescriptor.Undefined)
            {
                return Undefined;
            }

            if (desc.IsDataDescriptor())
            {
                var val = desc.Value;
                return val != null ? val : Undefined;
            }

            var getter = desc.Get != null ? desc.Get : Undefined;

            if (getter.IsUndefined())
            {
                return JsValue.Undefined;
            }

            // if getter is not undefined it must be ICallable
            var callable = getter.TryCast<ICallable>();
            return callable.Call(this, Arguments.Empty);
        }

        /// <summary>
        /// Returns the Property Descriptor of the named
        /// own property of this object, or undefined if
        /// absent.
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.1
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public virtual IPropertyDescriptor GetOwnProperty(string propertyName)
        {
            EnsureInitialized();

            if (propertyName == PropertyNamePrototype)
            {
                return _prototype ?? PropertyDescriptor.Undefined;
            }

            if (propertyName == PropertyNameConstructor)
            {
                return _constructor ?? PropertyDescriptor.Undefined;
            }

            if (propertyName == PropertyNameLength)
            {
                return _length ?? PropertyDescriptor.Undefined;
            }

            if (_properties != null && _properties.TryGetValue(propertyName, out var x))
            {
                return x;
            }

            return PropertyDescriptor.Undefined;
        }

        protected internal virtual void SetOwnProperty(string propertyName, IPropertyDescriptor desc)
        {
            EnsureInitialized();

            if (propertyName == PropertyNamePrototype)
            {
                _prototype = desc;
                return;
            }

            if (propertyName == PropertyNameConstructor)
            {
                _constructor = desc;
                return;
            }

            if (propertyName == PropertyNameLength)
            {
                _length = desc;
                return;
            }

            if (_properties == null)
            {
                _properties = new MruPropertyCache2<string, IPropertyDescriptor>();
            }

            _properties[propertyName] = desc;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.2
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public IPropertyDescriptor GetProperty(string propertyName)
        {
            var prop = GetOwnProperty(propertyName);

            if (prop != PropertyDescriptor.Undefined)
            {
                return prop;
            }

            if (Prototype == null)
            {
                return PropertyDescriptor.Undefined;
            }

            return Prototype.GetProperty(propertyName);
        }

        public bool TryGetValue(string propertyName, out JsValue value)
        {
            value = Undefined;
            var desc = GetOwnProperty(propertyName);
            if (desc != null && desc != PropertyDescriptor.Undefined)
            {
                if (desc == PropertyDescriptor.Undefined)
                {
                    return false;
                }

                if (desc.IsDataDescriptor() && desc.Value != null)
                {
                    value = desc.Value;
                    return true;
                }

                var getter = desc.Get != null ? desc.Get : Undefined;

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

            if (Prototype == null)
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
        public virtual void Put(string propertyName, JsValue value, bool throwOnError)
        {
            if (!CanPut(propertyName))
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(Engine.TypeError);
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
                var newDesc = new ConfigurableEnumerableWritablePropertyDescriptor(value);
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
        public bool CanPut(string propertyName)
        {
            var desc = GetOwnProperty(propertyName);

            if (desc != PropertyDescriptor.Undefined)
            {
                if (desc.IsAccessorDescriptor())
                {
                    if (desc.Set == null || desc.Set.IsUndefined())
                    {
                        return false;
                    }

                    return true;
                }

                return desc.Writable.HasValue && desc.Writable.Value;
            }

            if (Prototype == null)
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
                if (inherited.Set == null || inherited.Set.IsUndefined())
                {
                    return false;
                }

                return true;
            }

            if (!Extensible)
            {
                return false;
            }
            else
            {
                return inherited.Writable.HasValue && inherited.Writable.Value;
            }
        }

        /// <summary>
        /// Returns a Boolean value indicating whether the
        /// object already has a property with the given
        /// name.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public bool HasProperty(string propertyName)
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
        public virtual bool Delete(string propertyName, bool throwOnError)
        {
            var desc = GetOwnProperty(propertyName);

            if (desc == PropertyDescriptor.Undefined)
            {
                return true;
            }

            if (desc.Configurable.HasValue && desc.Configurable.Value)
            {
                RemoveOwnProperty(propertyName);
                return true;
            }
            else
            {
                if (throwOnError)
                {
                    throw new JavaScriptException(Engine.TypeError);
                }

                return false;
            }
        }

        /// <summary>
        /// Hint is a String. Returns a default value for the
        /// object.
        /// </summary>
        /// <param name="hint"></param>
        /// <returns></returns>
        public JsValue DefaultValue(Types hint)
        {
            EnsureInitialized();

            if (hint == Types.String || (hint == Types.None && Class == "Date"))
            {
                var toString = Get("toString").TryCast<ICallable>();
                if (toString != null)
                {
                    var str = toString.Call(this, Arguments.Empty);
                    if (str.IsPrimitive())
                    {
                        return str;
                    }
                }

                var valueOf = Get("valueOf").TryCast<ICallable>();
                if (valueOf != null)
                {
                    var val = valueOf.Call(this, Arguments.Empty);
                    if (val.IsPrimitive())
                    {
                        return val;
                    }
                }

                throw new JavaScriptException(Engine.TypeError);
            }

            if (hint == Types.Number || hint == Types.None)
            {
                var valueOf = Get("valueOf").TryCast<ICallable>();
                if (valueOf != null)
                {
                    var val = valueOf.Call(this, Arguments.Empty);
                    if (val.IsPrimitive())
                    {
                        return val;
                    }
                }

                var toString = Get("toString").TryCast<ICallable>();
                if (toString != null)
                {
                    var str = toString.Call(this, Arguments.Empty);
                    if (str.IsPrimitive())
                    {
                        return str;
                    }
                }

                throw new JavaScriptException(Engine.TypeError);
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
        public virtual bool DefineOwnProperty(string propertyName, IPropertyDescriptor desc, bool throwOnError)
        {
            var current = GetOwnProperty(propertyName);

            if (current == desc)
            {
                return true;
            }

            if (current == PropertyDescriptor.Undefined)
            {
                if (!Extensible)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }

                    return false;
                }
                else
                {
                    if (desc.IsGenericDescriptor() || desc.IsDataDescriptor())
                    {
                        IPropertyDescriptor propertyDescriptor;
                        if (desc.Configurable.GetValueOrDefault() && desc.Enumerable.GetValueOrDefault() && desc.Writable.GetValueOrDefault())
                        {
                            propertyDescriptor = new ConfigurableEnumerableWritablePropertyDescriptor(desc.Value != null ? desc.Value : Undefined);
                        }
                        else if (!desc.Configurable.GetValueOrDefault() && !desc.Enumerable.GetValueOrDefault() && !desc.Writable.GetValueOrDefault())
                        {
                            propertyDescriptor = new AllForbiddenPropertyDescriptor(desc.Value != null ? desc.Value : Undefined);
                        }
                        else
                        {
                            propertyDescriptor = new PropertyDescriptor(desc)
                            {
                                Value = desc.Value != null ? desc.Value : Undefined,
                                Writable = desc.Writable.HasValue ? desc.Writable.Value : false,
                                Enumerable = desc.Enumerable.HasValue ? desc.Enumerable.Value : false,
                                Configurable = desc.Configurable.HasValue ? desc.Configurable.Value : false
                            };
                        }
                        SetOwnProperty(propertyName, propertyDescriptor);
                    }
                    else
                    {
                        SetOwnProperty(propertyName, new PropertyDescriptor(desc)
                        {
                            Get = desc.Get,
                            Set = desc.Set,
                            Enumerable = desc.Enumerable.HasValue ? desc.Enumerable : false,
                            Configurable = desc.Configurable.HasValue ? desc.Configurable : false,
                        });
                    }
                }

                return true;
            }

            // Step 5
            if (!current.Configurable.HasValue &&
                !current.Enumerable.HasValue &&
                !current.Writable.HasValue &&
                current.Get == null &&
                current.Set == null &&
                current.Value == null)
            {
                return true;
            }

            // Step 6
            if (
                current.Configurable == desc.Configurable &&
                current.Writable == desc.Writable &&
                current.Enumerable == desc.Enumerable &&
                ((current.Get == null && desc.Get == null) || (current.Get != null && desc.Get != null && ExpressionInterpreter.SameValue(current.Get, desc.Get))) &&
                ((current.Set == null && desc.Set == null) || (current.Set != null && desc.Set != null && ExpressionInterpreter.SameValue(current.Set, desc.Set))) &&
                ((current.Value == null && desc.Value == null) || (current.Value != null && desc.Value != null && ExpressionInterpreter.StrictlyEqual(current.Value, desc.Value)))
            )
            {
                return true;
            }

            if (!current.Configurable.HasValue || !current.Configurable.Value)
            {
                if (desc.Configurable.HasValue && desc.Configurable.Value)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }

                    return false;
                }

                if (desc.Enumerable.HasValue && (!current.Enumerable.HasValue || desc.Enumerable.Value != current.Enumerable.Value))
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }

                    return false;
                }
            }

            if (!desc.IsGenericDescriptor())
            {
                if (current.IsDataDescriptor() != desc.IsDataDescriptor())
                {
                    if (!current.Configurable.HasValue || !current.Configurable.Value)
                    {
                        if (throwOnError)
                        {
                            throw new JavaScriptException(Engine.TypeError);
                        }

                        return false;
                    }

                    if (current.IsDataDescriptor())
                    {
                        SetOwnProperty(propertyName, current = new PropertyDescriptor(
                            get: JsValue.Undefined,
                            set: JsValue.Undefined,
                            enumerable: current.Enumerable,
                            configurable: current.Configurable
                        ));
                    }
                    else
                    {
                        SetOwnProperty(propertyName, current = new PropertyDescriptor(
                            value: JsValue.Undefined,
                            writable: null,
                            enumerable: current.Enumerable,
                            configurable: current.Configurable
                        ));
                    }
                }
                else if (current.IsDataDescriptor() && desc.IsDataDescriptor())
                {
                    if (!current.Configurable.HasValue || current.Configurable.Value == false)
                    {
                        if (!current.Writable.HasValue || !current.Writable.Value && desc.Writable.HasValue && desc.Writable.Value)
                        {
                            if (throwOnError)
                            {
                                throw new JavaScriptException(Engine.TypeError);
                            }

                            return false;
                        }

                        if (!current.Writable.Value)
                        {
                            if (desc.Value != null && !ExpressionInterpreter.SameValue(desc.Value, current.Value))
                            {
                                if (throwOnError)
                                {
                                    throw new JavaScriptException(Engine.TypeError);
                                }

                                return false;
                            }
                        }
                    }
                }
                else if (current.IsAccessorDescriptor() && desc.IsAccessorDescriptor())
                {
                    if (!current.Configurable.HasValue || !current.Configurable.Value)
                    {
                        if ((desc.Set != null && !ExpressionInterpreter.SameValue(desc.Set, current.Set != null ? current.Set : Undefined))
                            ||
                            (desc.Get != null && !ExpressionInterpreter.SameValue(desc.Get, current.Get != null ? current.Get : Undefined)))
                        {
                            if (throwOnError)
                            {
                                throw new JavaScriptException(Engine.TypeError);
                            }

                            return false;
                        }
                    }
                }
            }

            if (desc.Value != null)
            {
                current.Value = desc.Value;
            }

            PropertyDescriptor mutable = null;
            if (desc.Writable.HasValue)
            {
                current = mutable = current as PropertyDescriptor ?? new PropertyDescriptor(current);
                mutable.Writable = desc.Writable;
            }

            if (desc.Enumerable.HasValue)
            {
                current = mutable = current as PropertyDescriptor ?? new PropertyDescriptor(current);
                mutable.Enumerable = desc.Enumerable;
            }

            if (desc.Configurable.HasValue)
            {
                current = mutable = current as PropertyDescriptor ?? new PropertyDescriptor(current);
                mutable.Configurable = desc.Configurable;
            }

            if (desc.Get != null)
            {
                current = mutable = current as PropertyDescriptor ?? new PropertyDescriptor(current);
                mutable.Get = desc.Get;
            }

            if (desc.Set != null)
            {
                mutable = current as PropertyDescriptor ?? new PropertyDescriptor(current);
                mutable.Set = desc.Set;
            }

            if (mutable != null)
            {
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
        public void FastSetProperty(string name, IPropertyDescriptor value)
        {
            SetOwnProperty(name, value);
        }

        protected virtual void EnsureInitialized()
        {
        }

        public override string ToString()
        {
            return TypeConverter.ToString(this);
        }

        protected uint GetLengthValue() => TypeConverter.ToUint32(_length.Value);

                public override Types Type => Types.Object;

        [Pure]
        public override bool IsArray()
        {
            return this is ArrayInstance;
        }

        [Pure]
        public override bool IsDate()
        {
            return this is DateInstance;
        }

        [Pure]
        public override bool IsRegExp()
        {
            return this is RegExpInstance;
        }

        [Pure]
        public override ObjectInstance AsObject()
        {
            return this;
        }

        [Pure]
        public override TInstance AsInstance<TInstance>()
        {
            return this as TInstance;
        }

        [Pure]
        public override ArrayInstance AsArray()
        {
            if (!IsArray())
            {
                throw new ArgumentException("The value is not an array");
            }

            return this as ArrayInstance;
        }

        [Pure]
        public override DateInstance AsDate()
        {
            if (!IsDate())
            {
                throw new ArgumentException("The value is not a date");
            }

            return this as DateInstance;
        }

        [Pure]
        public override RegExpInstance AsRegExp()
        {
            if (!IsRegExp())
            {
                throw new ArgumentException("The value is not a regex");
            }

            return this as RegExpInstance;
        }

        public override bool Is<T>()
        {
            return this is T;
        }

        public override T As<T>()
        {
            return this as T;
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
                        return stringInstance.PrimitiveValue.AsString();
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
                        return booleanInstance.PrimitiveValue.AsBoolean();
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
                        return numberInstance.NumberData.AsNumber();
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
                                IDictionary<string, object> o = new Dictionary<string, object>();
#else
                    IDictionary<string, object> o = new ExpandoObject();
#endif

                    foreach (var p in GetOwnProperties())
                    {
                        if (!p.Value.Enumerable.HasValue || p.Value.Enumerable.Value == false)
                        {
                            continue;
                        }

                        o.Add(p.Key, Get(p.Key).ToObject());
                    }

                    return o;
            }


            return this;
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