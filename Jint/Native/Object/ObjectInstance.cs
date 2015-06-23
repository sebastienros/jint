using System.Collections.Generic;
using Jint.Native.Date;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object
{
    public class ObjectInstance
    {
        public ObjectInstance(Engine engine)
        {
            Engine = engine;
            Properties = new Dictionary<string, PropertyDescriptor>();
        }

        public Engine Engine { get; set; }

        public IDictionary<string, PropertyDescriptor> Properties { get; private set; }

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
                return desc.Value.HasValue ? desc.Value.Value : Undefined.Instance;
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
                var valueDesc = new PropertyDescriptor(value: value);
                DefineOwnProperty(propertyName, valueDesc, throwOnError);
                return;
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
                var newDesc = new PropertyDescriptor(value, DescriptorAttributes.All);
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

                return desc.Writable;
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
                return inherited.Writable;
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

            if (desc.Configurable)
            {
                Properties.Remove(propertyName);
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
                        Properties[propertyName] = new PropertyDescriptor(desc) {
                            Value = desc.Value.HasValue ? desc.Value : JsValue.Undefined
                        }.WithAttributes(desc.Writable, null, null);
                    }
                    else
                    {
                        Properties[propertyName] = new PropertyDescriptor(desc)
                        .WithAttributes(null, desc.Enumerable, desc.Configurable);
                }
                }

                return true;
            }

            // Step 5
            if (!current.Configurable && 
                !current.Enumerable &&
                !current.Writable &&
                !current.Get.HasValue &&
                !current.Set.HasValue &&
                !current.Value.HasValue)
            {
                return true;
            }

            // Step 6
            var configurableIsSame = current.Configurable
                ? desc.Configurable && (current.Configurable)
                : !desc.Configurable;

            var enumerableIsSame = current.Enumerable
                ? desc.Enumerable && (current.Enumerable)
                : !desc.Enumerable;

            var writableIsSame = true;
            var valueIsSame = true;

            if (current.IsDataDescriptor() && desc.IsDataDescriptor())
            {
                var currentDataDescriptor = current;
                var descDataDescriptor = desc;
                writableIsSame = currentDataDescriptor.Writable
                ? descDataDescriptor.Writable && (currentDataDescriptor.Writable == descDataDescriptor.Writable)
                : !descDataDescriptor.Writable;

                var valueA = currentDataDescriptor.Value.HasValue
                    ? currentDataDescriptor.Value.Value
                    : Undefined.Instance;

                var valueB = descDataDescriptor.Value.HasValue
                                    ? descDataDescriptor.Value.Value
                                    : Undefined.Instance;

                valueIsSame = ExpressionInterpreter.SameValue(valueA, valueB);
            }
            else if (current.IsAccessorDescriptor() && desc.IsAccessorDescriptor())
            {
                var currentAccessorDescriptor = current;
                var descAccessorDescriptor = desc;

                var getValueA = currentAccessorDescriptor.Get.HasValue
                    ? currentAccessorDescriptor.Get.Value
                    : Undefined.Instance;

                var getValueB = descAccessorDescriptor.Get.HasValue
                                    ? descAccessorDescriptor.Get.Value
                                    : Undefined.Instance;

                var setValueA = currentAccessorDescriptor.Set.HasValue
                   ? currentAccessorDescriptor.Set.Value
                   : Undefined.Instance;

                var setValueB = descAccessorDescriptor.Set.HasValue
                                    ? descAccessorDescriptor.Set.Value
                                    : Undefined.Instance;

                valueIsSame = ExpressionInterpreter.SameValue(getValueA, getValueB)
                              && ExpressionInterpreter.SameValue(setValueA, setValueB);
            }
            else
            {
                valueIsSame = false;
            }

            if (configurableIsSame && enumerableIsSame && writableIsSame && valueIsSame)
            {
                return true;
            }

            if (!current.Configurable)
            {
                if (desc.Configurable)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }

                    return false;
                }

                if (desc.Enumerable && (!current.Enumerable || desc.Enumerable!= current.Enumerable))
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
                    if (!current.Configurable)
                    {
                        if (throwOnError)
                        {
                            throw new JavaScriptException(Engine.TypeError);
                        }

                        return false;
                    }

                    if (current.IsDataDescriptor())
                    {
                        Properties[propertyName] = current = new PropertyDescriptor(
                            get: Undefined.Instance,
                            set: Undefined.Instance).WithAttributes(null,
                            enumerable: current.Enumerable,
                            configurable: current.Configurable
                            );
                    }
                    else
                    {
                        Properties[propertyName] = current = new PropertyDescriptor(
                            value: Undefined.Instance).WithAttributes(
                            writable: null,
                            enumerable: current.Enumerable,
                            configurable: current.Configurable
                            );
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
                                throw new JavaScriptException(Engine.TypeError);
                            }

                            return false;
                        }

                        if (!current.Writable)
                        {
                            if (desc.Value.HasValue && !valueIsSame)
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
                    if (!current.Configurable)
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

            if (desc.Writable)
            {
                current.WithWritable();
            }

            if (desc.Enumerable)
            {
                current.WithEnumerable();
            }

            if (desc.Configurable)
            {
                current.WithConfigurable();
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
            Properties.Add(name, new PropertyDescriptor(value).WithAttributes(writable, enumerable, configurable));
        }

        /// <summary>
        /// Optimized version of [[Put]] when the property is known to be already declared 
        /// </summary>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public void FastSetProperty(string name, PropertyDescriptor value)
        {
            Properties[name] = value;
        }

        public override string ToString()
        {
            return TypeConverter.ToString(this);
        }
    }
}
