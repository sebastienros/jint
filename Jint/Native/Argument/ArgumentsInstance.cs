using System;
using System.Collections.Generic;
using System.Threading;
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
        // cache key container for array iteration for less allocations
        private static readonly ThreadLocal<List<string>> _mappedNamed = new ThreadLocal<List<string>>(() => new List<string>());

        private readonly Action<ArgumentsInstance> _initializer;
        private bool _initialized;

        private ArgumentsInstance(Engine engine, Action<ArgumentsInstance> initializer) : base(engine)
        {
            _initializer = initializer;
            _initialized = false;
        }

        public bool Strict { get; set; }

        protected override void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            _initializer(this);
        }

        public static ArgumentsInstance CreateArgumentsObject(Engine engine, FunctionInstance func, string[] names, JsValue[] args, EnvironmentRecord env, bool strict)
        {
            void Initializer(ArgumentsInstance self)
            {
                var len = args.Length;
                self.SetOwnProperty("length", new NonEnumerablePropertyDescriptor(len));
                if (args.Length > 0)
                {
                    var map = engine.Object.Construct(Arguments.Empty);
                    var mappedNamed = _mappedNamed.Value;
                    mappedNamed.Clear();
                    for (var indx = 0; indx < len; indx++)
                    {
                        var indxStr = TypeConverter.ToString(indx);
                        var val = args[indx];
                        self.SetOwnProperty(indxStr, new ConfigurableEnumerableWritablePropertyDescriptor(val));
                        if (indx < names.Length)
                        {
                            var name = names[indx];
                            if (!strict && !mappedNamed.Contains(name))
                            {
                                mappedNamed.Add(name);
                                map.SetOwnProperty(indxStr, new ClrAccessDescriptor(env, engine, name));
                            }
                        }
                    }

                    // step 12
                    if (mappedNamed.Count > 0)
                    {
                        self.ParameterMap = map;
                    }
                }

                // step 13
                if (!strict)
                {
                    self.SetOwnProperty("callee", new NonEnumerablePropertyDescriptor(func));
                }
                // step 14
                else
                {
                    var thrower = engine.Function.ThrowTypeError;
                    self.DefineOwnProperty("caller", new PropertyDescriptor(get: thrower, set: thrower, enumerable: false, configurable: false), false);
                    self.DefineOwnProperty("callee", new PropertyDescriptor(get: thrower, set: thrower, enumerable: false, configurable: false), false);
                }
            }

            var obj = new ArgumentsInstance(engine, Initializer);

            // These properties are pre-initialized as their don't trigger
            // the EnsureInitialized() event and are cheap
            obj.Prototype = engine.Object.PrototypeObject;
            obj.Extensible = true;
            obj.Strict = strict;

            return obj;
        }

        public ObjectInstance ParameterMap { get; set; }

        public override string Class => "Arguments";

        public override IPropertyDescriptor GetOwnProperty(string propertyName)
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
                var valueDesc = new NullConfigurationPropertyDescriptor(value);
                DefineOwnProperty(propertyName, valueDesc, throwOnError);
                return;
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

        public override bool DefineOwnProperty(string propertyName, IPropertyDescriptor desc, bool throwOnError)
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
                        if (desc.Value != null && desc.Value != Undefined)
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