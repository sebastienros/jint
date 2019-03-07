using System;
using System.Globalization;
using System.Reflection;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    public sealed class IndexDescriptor : PropertyDescriptor
    {
        private readonly Engine _engine;
        private readonly object _key;
        private readonly object _item;
        private readonly PropertyInfo _indexer;
        private readonly MethodInfo _containsKey;

        public IndexDescriptor(Engine engine, Type targetType, string key, object item) : base(PropertyFlag.CustomJsValue)
        {
            _engine = engine;
            _item = item;

            // get all instance indexers with exactly 1 argument
            var indexers = targetType.GetProperties();
            var paramTypeArray = new Type[1];

            // try to find first indexer having either public getter or setter with matching argument type
            foreach (var indexer in indexers)
            {
                var indexParameters = indexer.GetIndexParameters();
                if (indexParameters.Length != 1)
                {
                    continue;
                }

                if (indexer.GetGetMethod() != null || indexer.GetSetMethod() != null)
                {
                    var paramType = indexParameters[0].ParameterType;

                    if (_engine.ClrTypeConverter.TryConvert(key, paramType, CultureInfo.InvariantCulture, out _key))
                    {
                        _indexer = indexer;
                        // get contains key method to avoid index exception being thrown in dictionaries
                        paramTypeArray[0] = paramType;
                        _containsKey = targetType.GetMethod("ContainsKey", paramTypeArray);
                        break;
                    }
                }
            }

            // throw if no indexer found
            if (_indexer == null)
            {
                ExceptionHelper.ThrowInvalidOperationException("No matching indexer found.");
            }

            Writable = engine.Options._IsClrWriteAllowed;
        }

        public IndexDescriptor(Engine engine, string key, object item)
            : this(engine, item.GetType(), key, item)
        {
        }

        protected internal override JsValue CustomValue
        {
            get
            {
                var getter = _indexer.GetGetMethod();

                if (getter == null)
                {
                    ExceptionHelper.ThrowInvalidOperationException("Indexer has no public getter.");
                }

                object[] parameters = {_key};

                if (_containsKey != null)
                {
                    if ((_containsKey.Invoke(_item, parameters) as bool?) != true)
                    {
                        return JsValue.Undefined;
                    }
                }

                try
                {
                    return JsValue.FromObject(_engine, getter.Invoke(_item, parameters));
                }
                catch
                {
                    return JsValue.Undefined;
                }
            }

            set
            {
                var setter = _indexer.GetSetMethod();
                if (setter == null)
                {
                    ExceptionHelper.ThrowInvalidOperationException("Indexer has no public setter.");
                }

                object[] parameters = {_key, value?.ToObject()};
                setter.Invoke(_item, parameters);
            }
        }
    }
}