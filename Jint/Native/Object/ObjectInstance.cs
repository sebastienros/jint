using System.Collections.Generic;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using System;

namespace Jint.Native.Object
{
    public class ObjectInstance
    {
        public ObjectInstance(Engine engine)
        {
            Engine = engine;
            Properties = new MruPropertyCache2<string, PropertyDescriptor>();
        }

        public Engine Engine { get; set; }

        protected IDictionary<string, PropertyDescriptor> Properties { get; private set; }

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
        public virtual string Class
        {
            get { return "Object"; }
        }

        public virtual IEnumerable<KeyValuePair<string, PropertyDescriptor>> GetOwnProperties()
        {
            EnsureInitialized();
            return Properties;
        }

        public virtual bool HasOwnProperty(string p)
        {
            EnsureInitialized();
            return Properties.ContainsKey(p);
        }

        public virtual void RemoveOwnProperty(string p)
        {
            EnsureInitialized();
            Properties.Remove(p);
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
                return JsValue.Undefined;
            }

            if (desc.IsDataDescriptor())
            {
                var val = desc.Value;
                return val.HasValue ? val.Value : Undefined.Instance;
            }

            var getter = desc.Get.HasValue ? desc.Get.Value : Undefined.Instance;

            if (getter.IsUndefined())
            {
                return Undefined.Instance;
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
        public virtual PropertyDescriptor GetOwnProperty(string propertyName)
        {
            EnsureInitialized();

            PropertyDescriptor x;
            if (Properties.TryGetValue(propertyName, out x))
            {
                /* Spec implementation
                PropertyDescriptor d;
                if (x.IsDataDescriptor())
                {
                    d = new PropertyDescriptor(x.As<DataDescriptor>());
                }
                else
                {
                    d = new PropertyDescriptor(x.As<AccessorDescriptor>());
                }

                return d;
                */

                // optimmized implementation
                return x;
            }
            
            return PropertyDescriptor.Undefined;
        }

        protected virtual void SetOwnProperty(string propertyName, PropertyDescriptor desc)
        {
            EnsureInitialized();
            Properties[propertyName] = desc;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-8.12.2
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public PropertyDescriptor GetProperty(string propertyName)
        {
            var prop = GetOwnProperty(propertyName);

            if (prop != PropertyDescriptor.Undefined)
            {
                return prop;
            }
            
            if(Prototype == null)
            {
                return PropertyDescriptor.Undefined;
            }

            return Prototype.GetProperty(propertyName);
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
                var setter = desc.Set.Value.TryCast<ICallable>();
                setter.Call(new JsValue(this), new [] {value});
            }
            else
            {
                var newDesc = new PropertyDescriptor(value, true, true, true);
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
                    if (!desc.Set.HasValue || desc.Set.Value.IsUndefined())
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
                if (!inherited.Set.HasValue || inherited.Set.Value.IsUndefined())
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
                    var str = toString.Call(new JsValue(this), Arguments.Empty);
                    if (str.IsPrimitive())
                    {
                        return str;
                    }
                }

                var valueOf = Get("valueOf").TryCast<ICallable>();
                if (valueOf != null)
                {
                    var val = valueOf.Call(new JsValue(this), Arguments.Empty);
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
                    var val = valueOf.Call(new JsValue(this), Arguments.Empty);
                    if (val.IsPrimitive())
                    {
                        return val;
                    }
                }

                var toString = Get("toString").TryCast<ICallable>();
                if (toString != null)
                {
                    var str = toString.Call(new JsValue(this), Arguments.Empty);
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
        public virtual bool DefineOwnProperty(string propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            var current = GetOwnProperty(propertyName);

            if (current == desc) {
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
                        SetOwnProperty(propertyName, new PropertyDescriptor(desc)
                        {
                            Value = desc.Value.HasValue ? desc.Value : JsValue.Undefined,
                            Writable = desc.Writable.HasValue ? desc.Writable.Value : false,
                            Enumerable = desc.Enumerable.HasValue ? desc.Enumerable.Value : false,
                            Configurable = desc.Configurable.HasValue ? desc.Configurable.Value : false
                        });
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
                !current.Get.HasValue &&
                !current.Set.HasValue &&
                !current.Value.HasValue)
            {

                return true;
            }

            // Step 6
            if (
                current.Configurable == desc.Configurable &&
                current.Writable == desc.Writable &&
                current.Enumerable == desc.Enumerable &&

                ((!current.Get.HasValue && !desc.Get.HasValue) || (current.Get.HasValue && desc.Get.HasValue && ExpressionInterpreter.SameValue(current.Get.Value, desc.Get.Value))) &&
                ((!current.Set.HasValue && !desc.Set.HasValue) || (current.Set.HasValue && desc.Set.HasValue && ExpressionInterpreter.SameValue(current.Set.Value, desc.Set.Value))) &&
                ((!current.Value.HasValue && !desc.Value.HasValue) || (current.Value.HasValue && desc.Value.HasValue && ExpressionInterpreter.StrictlyEqual(current.Value.Value, desc.Value.Value)))
            ) {
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
                            get: Undefined.Instance,
                            set: Undefined.Instance,
                            enumerable: current.Enumerable,
                            configurable: current.Configurable
                            ));
                    }
                    else
                    {
                        SetOwnProperty(propertyName, current = new PropertyDescriptor(
                            value: Undefined.Instance, 
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
                            if (desc.Value.HasValue && !ExpressionInterpreter.SameValue(desc.Value.Value, current.Value.Value))
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
                        if ((desc.Set.HasValue && !ExpressionInterpreter.SameValue(desc.Set.Value, current.Set.HasValue ? current.Set.Value : Undefined.Instance))
                            ||
                            (desc.Get.HasValue && !ExpressionInterpreter.SameValue(desc.Get.Value, current.Get.HasValue ? current.Get.Value : Undefined.Instance)))
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

            if (desc.Value.HasValue)
            {
                current.Value = desc.Value;
            }

            if (desc.Writable.HasValue)
            {
                current.Writable = desc.Writable;
            }

            if (desc.Enumerable.HasValue)
            {
                current.Enumerable = desc.Enumerable;
            }

            if (desc.Configurable.HasValue)
            {
                current.Configurable = desc.Configurable;
            }

            if (desc.Get.HasValue)
            {
                current.Get = desc.Get;
            }

            if (desc.Set.HasValue)
            {
                current.Set = desc.Set;
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
        public void FastSetProperty(string name, PropertyDescriptor value)
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
    }
}
