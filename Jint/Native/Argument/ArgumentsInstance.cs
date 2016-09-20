using System;
using System.Collections.Generic;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;

namespace Jint.Native.Argument
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.6
    /// </summary>
    public class ArgumentsInstance : ObjectInstance
    {
        private ArgumentsInstance(Engine engine, Action<ArgumentsInstance> initializer) : base(engine)
        {
            _initializer = initializer;
            _initialized = false;
        }

        public bool Strict { get; set; }

        private Action<ArgumentsInstance> _initializer;
        private bool _initialized;

        protected override void EnsureInitialized()
        {
            if(_initialized)
            {
                return;
            }

            _initialized = true;

            _initializer(this);
        }

        public static ArgumentsInstance CreateArgumentsObject(Engine engine, FunctionInstance func, string[] names, JsValue[] args, EnvironmentRecord env, bool strict)
        {
            var obj = new ArgumentsInstance(engine, self =>
            {
                var len = args.Length;
                self.FastAddProperty("length", len, true, false, true);
                var map = engine.Object.Construct(Arguments.Empty);
                var mappedNamed = new List<string>();
                var indx = 0;
                while (indx <= len - 1)
                {
                    var indxStr = TypeConverter.ToString(indx);
                    var val = args[indx];
                    self.FastAddProperty(indxStr, val, true, true, true);
                    if (indx < names.Length)
                    {
                        var name = names[indx];
                        if (!strict && !mappedNamed.Contains(name))
                        {
                            mappedNamed.Add(name);
                            Func<JsValue, JsValue> g = n => env.GetBindingValue(name, false);
                            var p = new Action<JsValue, JsValue>((n, o) => env.SetMutableBinding(name, o, true));

                            map.DefineOwnProperty(indxStr, new ClrAccessDescriptor(engine, g, p) { Configurable = true }, false);
                        }
                    }
                    indx++;
                }

                // step 12
                if (mappedNamed.Count > 0)
                {
                    self.ParameterMap = map;
                }

                // step 13
                if (!strict)
                {
                    self.FastAddProperty("callee", func, true, false, true);
                }
                // step 14
                else
                {
                    var thrower = engine.Function.ThrowTypeError;
                    self.DefineOwnProperty("caller", new PropertyDescriptor(get: thrower, set: thrower, enumerable: false, configurable: false), false);
                    self.DefineOwnProperty("callee", new PropertyDescriptor(get: thrower, set: thrower, enumerable: false, configurable: false), false);
                }
            });

            // These properties are pre-initialized as their don't trigger
            // the EnsureInitialized() event and are cheap
            obj.Prototype = engine.Object.PrototypeObject;
            obj.Extensible = true;
            obj.Strict = strict;


            return obj;
        }

        public ObjectInstance ParameterMap { get; set; }

        public override string Class
        {
            get
            {
                return "Arguments";
            }
        }


        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            EnsureInitialized();

            if (!Strict && ParameterMap != null)
            {
                var desc = base.GetOwnProperty(propertyName);
                if (desc == PropertyDescriptor.Undefined)
                {
                    return desc;
                }

                var isMapped = ParameterMap.GetOwnProperty(propertyName);
                if (isMapped != PropertyDescriptor.Undefined)
                {
                    desc.Value = ParameterMap.Get(propertyName);
                }

                return desc;
            }

            return base.GetOwnProperty(propertyName);
        }

        /// Implementation from ObjectInstance official specs as the one
        /// in ObjectInstance is optimized for the general case and wouldn't work
        /// for arrays
        public override void Put(string propertyName, JsValue value, bool throwOnError)
        {
            EnsureInitialized();

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
                var valueDesc = new PropertyDescriptor(value: value, writable: null, enumerable: null, configurable: null);
                DefineOwnProperty(propertyName, valueDesc, throwOnError);
                return;
            }

            // property is an accessor or inherited
            var desc = GetProperty(propertyName);

            if (desc.IsAccessorDescriptor())
            {
                var setter = desc.Set.TryCast<ICallable>();
                setter.Call(new JsValue(this), new[] { value });
            }
            else
            {
                var newDesc = new PropertyDescriptor(value, true, true, true);
                DefineOwnProperty(propertyName, newDesc, throwOnError);
            }
        }

        public override bool DefineOwnProperty(string propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            EnsureInitialized();

            if (!Strict && ParameterMap != null)
            {
                var map = ParameterMap;
                var isMapped = map.GetOwnProperty(propertyName);
                var allowed = base.DefineOwnProperty(propertyName, desc, false);
                if (!allowed)
                {
                    if (throwOnError)
                    {
                        throw new JavaScriptException(Engine.TypeError);
                    }
                }
                if (isMapped != PropertyDescriptor.Undefined)
                {
                    if (desc.IsAccessorDescriptor())
                    {
                        map.Delete(propertyName, false);
                    }
                    else
                    {
                        if (desc.Value != null && desc.Value != Undefined.Instance)
                        {
                            map.Put(propertyName, desc.Value, throwOnError);
                        }

                        if (desc.Writable.HasValue && desc.Writable.Value == false)
                        {
                            map.Delete(propertyName, false);
                        }
                    }
                }

                return true;
            }

            return base.DefineOwnProperty(propertyName, desc, throwOnError);
        }

        public override bool Delete(string propertyName, bool throwOnError)
        {
            EnsureInitialized();

            if (!Strict && ParameterMap != null)
            {
                var map = ParameterMap;
                var isMapped = map.GetOwnProperty(propertyName);
                var result = base.Delete(propertyName, throwOnError);
                if (result && isMapped != PropertyDescriptor.Undefined)
                {
                    map.Delete(propertyName, false);
                }

                return result;
            }

            return base.Delete(propertyName, throwOnError);
        }
    }
}
