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
        public virtual object Get(string propertyName)
        {
            var desc = GetProperty(propertyName);

            if (desc == PropertyDescriptor.Undefined)
            {
                return Undefined.Instance;
            }

            if (desc.IsDataDescriptor())
            {
                return desc.As<DataDescriptor>().Value;
            }

            var getter = desc.As<AccessorDescriptor>().Get;

            if (getter == Undefined.Instance)
            {
                return Undefined.Instance;
            }

            // if getter is not undefined it must be ICallable
            var callable = (ICallable)getter;
            return callable.Call(this, Arguments.Empty);
        }

        public void Set(string name, object value)
        {

            if (!HasProperty(name))
            {
                DefineOwnProperty(name, new DataDescriptor(value) { Configurable = true, Enumerable = true, Writable = true }, false);
            }
            else
            {
                Put(name, value, false);
            }
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
                    d = new DataDescriptor(x.As<DataDescriptor>());
                }
                else
                {
                    d = new AccessorDescriptor(x.As<AccessorDescriptor>());
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
        public void Put(string propertyName, object value, bool throwOnError)
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
                var valueDesc = new DataDescriptor(value);
                DefineOwnProperty(propertyName, valueDesc, throwOnError);
                return;
            }

            // property is an accessor or inherited
            var desc = GetProperty(propertyName);

            if (desc.IsAccessorDescriptor())
            {
                var setter = (ICallable)desc.As<AccessorDescriptor>().Set;
                setter.Call(this, new [] {value});
            }
            else
            {
                var newDesc = new DataDescriptor(value) {Writable = true, Enumerable = true, Configurable = true};
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
                    if (desc.As<AccessorDescriptor>().Set == Undefined.Instance)
                    {
                        return false;
                    }

                    return true;
                }

                return desc.As<DataDescriptor>().WritableIsSet;
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
                if (inherited.As<AccessorDescriptor>().Set == Undefined.Instance)
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
                return inherited.As<DataDescriptor>().WritableIsSet;
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

            if (desc.ConfigurableIsSetToTrue)
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
        public object DefaultValue(Types hint)
        {
            if ((hint == Types.String) || (hint == Types.None && this is StringInstance) || this is DateInstance)
            {
                var toString = Get("toString");
                var callable = toString as ICallable;
                if (callable != null)
                {
                    var str = callable.Call(this, Arguments.Empty);
                    if (TypeConverter.IsPrimitiveValue(str))
                    {
                        return str;
                    }
                }

                var valueOf = Get("valueOf");
                callable = valueOf as ICallable;
                if (callable != null)
                {
                    var val = callable.Call(this, Arguments.Empty);
                    if (TypeConverter.IsPrimitiveValue(val))
                    {
                        return val;
                    }
                }

                throw new JavaScriptException(Engine.TypeError);
            }

            if ((hint == Types.Number) || (hint == Types.None))
            {
                var valueOf = Get("valueOf");
                var callable = valueOf as ICallable;
                if (callable != null)
                {
                    var val = callable.Call(this, Arguments.Empty);
                    if (TypeConverter.IsPrimitiveValue(val))
                    {
                        return val;
                    }
                }

                var toString = Get("toString");
                callable = toString as ICallable;
                if (callable != null)
                {
                    var str = callable.Call(this, Arguments.Empty);
                    if (TypeConverter.IsPrimitiveValue(str))
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
                        Properties[propertyName] = new DataDescriptor(desc.As<DataDescriptor>());
                    }
                    else
                    {
                        Properties[propertyName] = new AccessorDescriptor(desc.As<AccessorDescriptor>());
                    }
                }

                return true;
            }

            // Step 5
            if(!current.Configurable.HasValue && !current.Enumerable.HasValue && !(current.IsDataDescriptor() && ((DataDescriptor)current).Writable.HasValue))
            {
                if (!desc.IsDataDescriptor() || desc.As<DataDescriptor>().Value == null)
                {
                    return true;
                }
            }

            // Step 6
            var configurableIsSame = current.Configurable.HasValue
                ? desc.Configurable.HasValue && (current.Configurable.Value == desc.Configurable.Value)
                : !desc.Configurable.HasValue;

            var enumerableIsSame = current.Enumerable.HasValue
                ? desc.Enumerable.HasValue && (current.Enumerable.Value == desc.Enumerable.Value)
                : !desc.Enumerable.HasValue;

            var writableIsSame = true;
            var valueIsSame = true;

            if (current.IsDataDescriptor() && desc.IsDataDescriptor())
            {
                var currentDataDescriptor = (DataDescriptor) current;
                var descDataDescriptor = (DataDescriptor) desc;
                writableIsSame = currentDataDescriptor.Writable.HasValue
                ? descDataDescriptor.Writable.HasValue && (currentDataDescriptor.Writable.Value == descDataDescriptor.Writable.Value)
                : !descDataDescriptor.Writable.HasValue;

                valueIsSame = ExpressionInterpreter.SameValue(currentDataDescriptor.Value, descDataDescriptor.Value);
            }
            else if (current.IsAccessorDescriptor() && desc.IsAccessorDescriptor())
            {
                var currentAccessorDescriptor = (AccessorDescriptor) current;
                var descAccessorDescriptor = (AccessorDescriptor) desc;

                valueIsSame = ExpressionInterpreter.SameValue(currentAccessorDescriptor.Get, descAccessorDescriptor.Get)
                              && ExpressionInterpreter.SameValue(currentAccessorDescriptor.Set, descAccessorDescriptor.Set);
            }
            else
            {
                valueIsSame = false;
            }

            if (configurableIsSame && enumerableIsSame && writableIsSame && valueIsSame)
            {
                return true;
            }

            if (current.ConfigurableIsSetToFalse)
            {
                if (desc.ConfigurableIsSetToTrue)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }

                    return false;
                }

                if (desc.Enumerable.HasValue && desc.EnumerableIsSet != current.EnumerableIsSet)
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
                    if (!current.ConfigurableIsSetToTrue)
                    {
                        if (throwOnError)
                        {
                            throw new JavaScriptException(Engine.TypeError);
                        }

                        return false;
                    }

                    if (current.IsDataDescriptor())
                    {
                        // todo: convert to accessor
                    }
                }
                else if (current.IsDataDescriptor() && desc.IsDataDescriptor())
                {
                    var cd = current.As<DataDescriptor>();
                    var dd = current.As<DataDescriptor>();

                    if (!current.ConfigurableIsSetToTrue)
                    {
                        if (!cd.WritableIsSet && dd.WritableIsSet)
                        {
                            if (throwOnError)
                            {
                                throw new JavaScriptException(Engine.TypeError);
                            }

                            return false;
                        }

                        if (!cd.WritableIsSet)
                        {
                            if (dd.Value != null && !valueIsSame)
                            {
                                if (throwOnError)
                                {
                                    throw new JavaScriptException(Engine.TypeError);
                                }

                                return false;
                            }
                        }
                    }

                    if (!dd.Writable.HasValue && cd.Writable.HasValue)
                    {
                        dd.Enumerable = cd.Enumerable;
                    }
                }
                else if (current.IsAccessorDescriptor() && desc.IsAccessorDescriptor())
                {
                    var ca = current.As<AccessorDescriptor>();
                    var da = desc.As<AccessorDescriptor>();

                    if (!current.ConfigurableIsSetToTrue)
                    {
                        if ((da.Set != Undefined.Instance && !ExpressionInterpreter.SameValue(da.Set, ca.Set))
                            || (da.Get != Undefined.Instance && !ExpressionInterpreter.SameValue(da.Get, ca.Get)))
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

            Properties[propertyName] = desc;

            if (!desc.Configurable.HasValue && current.Configurable.HasValue)
            {
                desc.Configurable = current.Configurable;
            }

            if (!desc.Enumerable.HasValue && current.Enumerable.HasValue)
            {
                desc.Enumerable = current.Enumerable;
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
        public void FastAddProperty(string name, object value, bool writable, bool enumerable, bool configurable)
        {
            Properties.Add(name, new DataDescriptor(value) { Writable = writable, Enumerable = enumerable, Configurable = configurable });
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
