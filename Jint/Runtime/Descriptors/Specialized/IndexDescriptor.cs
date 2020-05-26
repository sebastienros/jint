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
        private readonly object _target;
        private readonly PropertyInfo _indexer;
        private readonly MethodInfo _containsKey;

        public IndexDescriptor(Engine engine, Type targetType, string key, object target) : base(PropertyFlag.CustomJsValue)
        {
            _engine = engine;
            _target = target;

            if (!TryFindIndexer(engine, targetType, key, out _indexer, out _containsKey, out _key))
            {
                ExceptionHelper.ThrowArgumentException("invalid indexing configuration, target indexer not found");
            }

            Writable = engine.Options._IsClrWriteAllowed;
        }

        public IndexDescriptor(Engine engine, string key, object item)
            : this(engine, item.GetType(), key, item)
        {
        }

        internal static bool TryFindIndexer(
            Engine engine,
            Type targetType,
            string propertyName,
            out PropertyInfo indexerProperty,
            out MethodInfo containsKeyMethod,
            out object indexerKey)
        {
            // get all instance indexers with exactly 1 argument
            var paramTypeArray = new Type[1];

            // try to find first indexer having either public getter or setter with matching argument type
            foreach (var candidate in targetType.GetProperties())
            {
                var indexParameters = candidate.GetIndexParameters();
                if (indexParameters.Length != 1)
                {
                    continue;
                }

                if (candidate.GetGetMethod() != null || candidate.GetSetMethod() != null)
                {
                    var paramType = indexParameters[0].ParameterType;

                    if (engine.ClrTypeConverter.TryConvert(propertyName, paramType, CultureInfo.InvariantCulture, out indexerKey))
                    {
                        indexerProperty = candidate;
                        // get contains key method to avoid index exception being thrown in dictionaries
                        paramTypeArray[0] = paramType;
                        containsKeyMethod = targetType.GetMethod("ContainsKey", paramTypeArray);
                        return true;
                    }
                }
            }

            indexerProperty = default;
            containsKeyMethod = default;
            indexerKey = default;
            return false;
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

                object[] parameters = { _key };

                if (_containsKey != null)
                {
                    if ((_containsKey.Invoke(_target, parameters) as bool?) != true)
                    {
                        return JsValue.Undefined;
                    }
                }

                try
                {
                    return JsValue.FromObject(_engine, getter.Invoke(_target, parameters));
                }
                catch (TargetInvocationException tie)
                {
                    switch (tie.InnerException)
                    {
                        case null:
                            throw;
                        case ArgumentOutOfRangeException _:
                            return JsValue.Undefined;
                        case IndexOutOfRangeException _:
                            return JsValue.Undefined;
                        default:
                            throw tie.InnerException;
                    }
                }
            }
            set
            {
                var setter = _indexer.GetSetMethod();
                if (setter == null)
                {
                    ExceptionHelper.ThrowInvalidOperationException("Indexer has no public setter.");
                }

                object[] parameters = { _key,  value?.ToObject() };
                try
                {
                    setter!.Invoke(_target, parameters);
                }
                catch (TargetInvocationException tie)
                {
                    if (tie.InnerException != null)
                    {
                        throw tie.InnerException;
                    }
                    throw;
                }
            }
        }
    }
}