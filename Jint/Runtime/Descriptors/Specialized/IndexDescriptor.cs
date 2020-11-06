using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    internal sealed class IndexDescriptor : PropertyDescriptor
    {
        private readonly Engine _engine;
        private readonly object _key;
        private readonly object _target;
        private readonly PropertyInfo _indexer;
        private readonly MethodInfo _containsKey;

        private static readonly PropertyInfo _iListIndexer = typeof(IList).GetProperty("Item");

        internal IndexDescriptor(Engine engine, object target, PropertyInfo indexer, MethodInfo containsKey, object key)
            : base(PropertyFlag.Enumerable | PropertyFlag.CustomJsValue)
        {
            _engine = engine;
            _target = target;
            _indexer = indexer;
            _containsKey = containsKey;
            _key = key;
            Writable = engine.Options._IsClrWriteAllowed;
        }

        internal static bool TryFindIndexer(
            Engine engine,
            Type targetType,
            string propertyName,
            out Func<object, IndexDescriptor> factory)
        {
            var paramTypeArray = new Type[1];
            Func<object, IndexDescriptor> ComposeIndexerFactory(PropertyInfo candidate, Type paramType)
            {
                if (engine.ClrTypeConverter.TryConvert(propertyName, paramType, CultureInfo.InvariantCulture, out var key))
                {
                    // the key can be converted for this indexer
                    var indexerProperty = candidate;
                    // get contains key method to avoid index exception being thrown in dictionaries
                    paramTypeArray[0] = paramType;
                    var containsKeyMethod = targetType.GetMethod("ContainsKey", paramTypeArray);
                    return (target) => new IndexDescriptor(engine, target, indexerProperty, containsKeyMethod, key);
                }

                // the key type doesn't work for this indexer
                return null;
            }

            // default indexer wins
            if (typeof(IList).IsAssignableFrom(targetType))
            {
                factory = ComposeIndexerFactory(_iListIndexer, typeof(int));
                if (factory != null)
                {
                    return true;
                }
            }

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
                    factory = ComposeIndexerFactory(candidate, paramType);
                    if (factory != null)
                    {
                        return true;
                    }
                }
            }

            factory = default;
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
                        case IndexOutOfRangeException _:
                        case InvalidOperationException _:
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

                var obj = value?.ToObject();
                
                // attempt to convert to expected type
                if (obj != null && obj.GetType() != _indexer.PropertyType)
                {
                    obj = _engine.ClrTypeConverter.Convert(obj, _indexer.PropertyType, CultureInfo.InvariantCulture);
                }
                
                object[] parameters = { _key,  obj };
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