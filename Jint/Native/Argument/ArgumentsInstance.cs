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
        internal JsValue[] _args;
        private EnvironmentRecord _env;
        private bool _strict;

        internal bool _initialized;

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

        protected override void Initialize()
        {
            var args = _args;
            SetOwnProperty(KnownKeys.Length, new PropertyDescriptor(args.Length, PropertyFlag.NonEnumerable));

            ObjectInstance map = null;
            if (args.Length > 0)
            {
                HashSet<string> mappedNamed = null;
                if (!_strict)
                {
                    mappedNamed = _mappedNamed.Value;
                    mappedNamed.Clear();
                }
                for (var i = 0; i < (uint) args.Length; i++)
                {
                    var indexKey = i < Key.indexKeys.Length
                        ? Key.indexKeys[i]
                        : (Key) TypeConverter.ToString(i);

                    var val = args[i];
                    SetOwnProperty(indexKey, new PropertyDescriptor(val, PropertyFlag.ConfigurableEnumerableWritable));
                    if (i < _names.Length)
                    {
                        var name = _names[i];
                        if (!_strict && !mappedNamed.Contains(name))
                        {
                            map = map ?? Engine.Object.Construct(Arguments.Empty);
                            mappedNamed.Add(name);
                            map.SetOwnProperty(indexKey, new ClrAccessDescriptor(_env, Engine, name));
                        }
                    }
                }
            }

            ParameterMap = map;

            // step 13
            if (!_strict)
            {
                SetOwnProperty(KnownKeys.Callee, new PropertyDescriptor(_func, PropertyFlag.NonEnumerable));
            }
            // step 14
            else
            {
                DefineOwnProperty(KnownKeys.Caller, _engine._getSetThrower, false);
                DefineOwnProperty(KnownKeys.Callee, _engine._getSetThrower, false);
            }
        }

        public ObjectInstance ParameterMap { get; set; }

        public override PropertyDescriptor GetOwnProperty(in Key propertyName)
        {
            EnsureInitialized();

            if (!_strict && !ReferenceEquals(ParameterMap, null))
            {
                var desc = base.GetOwnProperty(propertyName);
                if (desc == PropertyDescriptor.Undefined)
                {
                    return desc;
                }

                if (ParameterMap.TryGetValue(propertyName, out var jsValue) && !jsValue.IsUndefined())
                {
                    desc.Value = jsValue;
                }

                return desc;
            }

            return base.GetOwnProperty(propertyName);
        }

        /// Implementation from ObjectInstance official specs as the one
        /// in ObjectInstance is optimized for the general case and wouldn't work
        /// for arrays
        public override void Put(in Key propertyName, JsValue value, bool throwOnError)
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

        public override bool DefineOwnProperty(in Key propertyName, PropertyDescriptor desc, bool throwOnError)
        {
            if (_func is ScriptFunctionInstance scriptFunctionInstance && scriptFunctionInstance._function._hasRestParameter)
            {
                // immutable
                return false;
            }

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

        public override bool Delete(in Key propertyName, bool throwOnError)
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

        internal void PersistArguments()
        {
            EnsureInitialized();

            var args = _args;
            var copiedArgs = new JsValue[args.Length];
            System.Array.Copy(args, copiedArgs, args.Length);
            _args = copiedArgs;

            // should no longer expose arguments which is special name
            ParameterMap = null;
        }
    }
}