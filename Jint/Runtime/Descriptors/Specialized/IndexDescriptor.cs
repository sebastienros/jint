using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized
{
    internal sealed class IndexDescriptor : ReflectionPropertyDescriptor
    {
        private readonly object _key;

        private readonly MethodInfo _getter;
        private readonly MethodInfo _setter;
        private readonly MethodInfo _containsKey;

        private static readonly PropertyInfo _iListIndexer = typeof(IList).GetProperty("Item");

        private IndexDescriptor(Engine engine, object target, PropertyInfo indexer, MethodInfo containsKey, object key)
            : base(engine, indexer.PropertyType, target, indexer.CanWrite, indexerToTry: null)
        {
            _containsKey = containsKey;
            _key = key;

            _getter = indexer.GetGetMethod();
            _setter = indexer.GetSetMethod();
        }

        internal static bool TryFindIndexer(
            Engine engine,
            Type targetType,
            string propertyName,
            out Func<object, IndexDescriptor> factory,
            out PropertyInfo indexer)
        {
            var paramTypeArray = new Type[1];

            Func<object, IndexDescriptor> ComposeIndexerFactory(PropertyInfo candidate, Type paramType)
            {
                if (engine.ClrTypeConverter.TryConvert(propertyName, paramType, CultureInfo.InvariantCulture,
                    out var key))
                {
                    // the key can be converted for this indexer
                    var indexerProperty = candidate;
                    // get contains key method to avoid index exception being thrown in dictionaries
                    paramTypeArray[0] = paramType;
                    var containsKeyMethod = targetType.GetMethod(nameof(IDictionary<string, string>.ContainsKey), paramTypeArray);
                    if (containsKeyMethod is null)
                    {
                        paramTypeArray[0] = typeof(object);
                        containsKeyMethod = targetType.GetMethod(nameof(IDictionary.Contains), paramTypeArray);
                    }

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
                    indexer = _iListIndexer;
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
                        indexer = candidate;
                        return true;
                    }
                }
            }

            factory = default;
            indexer = default;
            return false;
        }

        protected override object DoGetValue(object target)
        {
            if (_getter is null)
            {
                ExceptionHelper.ThrowInvalidOperationException("Indexer has no public getter.");
                return null;
            }

            object[] parameters = {_key};

            if (_containsKey != null)
            {
                if (_containsKey.Invoke(target, parameters) as bool? != true)
                {
                    return JsValue.Undefined;
                }
            }

            return _getter.Invoke(target, parameters);
        }

        protected override void DoSetValue(object target, object value)
        {
            if (_setter is null)
            {
                ExceptionHelper.ThrowInvalidOperationException("Indexer has no public setter.");
            }

            object[] parameters = {_key, value};
            _setter!.Invoke(target, parameters);
        }
    }
}