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
    public sealed class ArgumentsInstance : ObjectInstance
    {
        // cache key container for array iteration for less allocations
        private static readonly ThreadLocal<HashSet<string>> _mappedNamed = new ThreadLocal<HashSet<string>>(() => new HashSet<string>());

        private FunctionInstance _func;
        private string[] _names;
        private JsValue[] _args;
        private EnvironmentRecord _env;
        private bool _strict;

        private bool _initialized;

        internal ArgumentsInstance(Engine engine) : base(engine, objectClass: "Arguments")
        {
        }

        internal void Prepare(
            FunctionInstance func, 
            string[] names, 
            JsValue[] args, 
            EnvironmentRecord env, 
            bool strict)
        {
            _func = func;
            _names = names;
            _args = args;
            _env = env;
            _strict = strict;

            _properties?.Clear();
            
            _initialized = false;
        }

        protected override void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;

            var self = this;
            var len = _args.Length;
            self.SetOwnProperty("length", new PropertyDescriptor(len, PropertyFlag.NonEnumerable));
            if (_args.Length > 0)
            {
                var map = Engine.Object.Construct(Arguments.Empty);
                var mappedNamed = _mappedNamed.Value;
                mappedNamed.Clear();
                for (var indx = 0; indx < len; indx++)
                {
                    var indxStr = TypeConverter.ToString(indx);
                    var val = _args[indx];
                    self.SetOwnProperty(indxStr, new PropertyDescriptor(val, PropertyFlag.ConfigurableEnumerableWritable));
                    if (indx < _names.Length)
                    {
                        var name = _names[indx];
                        if (!_strict && !mappedNamed.Contains(name))
                        {
                            mappedNamed.Add(name);
                            map.SetOwnProperty(indxStr, new ClrAccessDescriptor(_env, Engine, name));
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
            if (!_strict)
            {
                self.SetOwnProperty("callee", new PropertyDescriptor(_func, PropertyFlag.NonEnumerable));
            }
            // step 14
            else
            {
                var thrower = Engine.Function.ThrowTypeError;
                const PropertyFlag flags = PropertyFlag.EnumerableSet | PropertyFlag.ConfigurableSet;
                self.DefineOwnProperty("caller", new GetSetPropertyDescriptor(get: thrower, set: thrower, flags), false);
                self.DefineOwnProperty("callee", new GetSetPropertyDescriptor(get: thrower, set: thrower, flags), false);
            }
        }

        public ObjectInstance ParameterMap { get; set; }

        public override PropertyDescriptor GetOwnProperty(string propertyName)
        {
            EnsureInitialized();

            if (!_strict && !ReferenceEquals(ParameterMap, null))
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
                    ExceptionHelper.ThrowTypeError(Engine);
                }

                return;
            }

            var ownDesc = GetOwnProperty(propertyName);

            if (ownDesc.IsDataDescriptor())
            {
                var valueDesc = new PropertyDescriptor(value, PropertyFlag.None);
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
                var newDesc = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
                DefineOwnProperty(propertyName, newDesc, throwOnError);
            }
        }

        public override bool DefineOwnProperty(string propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            EnsureInitialized();

            if (!_strict && !ReferenceEquals(ParameterMap, null))
            {
                var map = ParameterMap;
                var isMapped = map.GetOwnProperty(propertyName);
                var allowed = base.DefineOwnProperty(propertyName, desc, false);
                if (!allowed)
                {
                    if (throwOnError)
                    {
                        ExceptionHelper.ThrowTypeError(Engine);
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
                        var descValue = desc.Value;
                        if (!ReferenceEquals(descValue, null) && !descValue.IsUndefined())
                        {
                            map.Put(propertyName, descValue, throwOnError);
                        }

                        if (desc.WritableSet && !desc.Writable)
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

            if (!_strict && !ReferenceEquals(ParameterMap, null))
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