using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Interop.Reflection
{
    internal sealed class IndexerAccessor : ReflectionAccessor
    {
        private readonly object _key;

        private readonly PropertyInfo _indexer;
        private readonly MethodInfo? _getter;
        private readonly MethodInfo? _setter;
        private readonly MethodInfo? _containsKey;

        private static readonly PropertyInfo _iListIndexer = typeof(IList).GetProperty("Item")!;

        private IndexerAccessor(PropertyInfo indexer, MethodInfo? containsKey, object key)
            : base(indexer.PropertyType, key)
        {
            _indexer = indexer;
            _containsKey = containsKey;
            _key = key;

            _getter = indexer.GetGetMethod();
            _setter = indexer.GetSetMethod();
        }

        internal static bool TryFindIndexer(
            Engine engine,
            Type targetType,
            string propertyName,
            [NotNullWhen(true)] out IndexerAccessor? indexerAccessor,
            [NotNullWhen(true)] out PropertyInfo? indexer)
        {
            var paramTypeArray = new Type[1];

            IndexerAccessor? ComposeIndexerFactory(PropertyInfo candidate, Type paramType)
            {
                object? key = null;
                // int key is quite common case
                if (paramType == typeof(int))
                {
                    if (int.TryParse(propertyName, out var intValue))
                    {
                        key = intValue;
                    }
                }
                else
                {
                    engine.ClrTypeConverter.TryConvert(propertyName, paramType, CultureInfo.InvariantCulture, out key);
                }

                if (key is not null)
                {
                    // the key can be converted for this indexer
                    var indexerProperty = candidate;
                    // get contains key method to avoid index exception being thrown in dictionaries
                    paramTypeArray[0] = paramType;
                    var containsKeyMethod = targetType.GetMethod(nameof(IDictionary<string, string>.ContainsKey), paramTypeArray);
                    if (containsKeyMethod is null && targetType.IsAssignableFrom(typeof(IDictionary)))
                    {
                        paramTypeArray[0] = typeof(object);
                        containsKeyMethod = targetType.GetMethod(nameof(IDictionary.Contains), paramTypeArray);
                    }

                    return new IndexerAccessor(indexerProperty, containsKeyMethod, key);
                }

                // the key type doesn't work for this indexer
                return null;
            }

            var filter = new Func<MemberInfo, bool>(m => engine.Options.Interop.TypeResolver.Filter(engine, m));

            // default indexer wins
            if (typeof(IList).IsAssignableFrom(targetType) && filter(_iListIndexer))
            {
                indexerAccessor = ComposeIndexerFactory(_iListIndexer, typeof(int));
                if (indexerAccessor != null)
                {
                    indexer = _iListIndexer;
                    return true;
                }
            }

            // try to find first indexer having either public getter or setter with matching argument type
            foreach (var candidate in targetType.GetProperties())
            {
                if (!filter(candidate))
                {
                    continue;
                }

                var indexParameters = candidate.GetIndexParameters();
                if (indexParameters.Length != 1)
                {
                    continue;
                }

                if (candidate.GetGetMethod() != null || candidate.GetSetMethod() != null)
                {
                    var paramType = indexParameters[0].ParameterType;
                    indexerAccessor = ComposeIndexerFactory(candidate, paramType);
                    if (indexerAccessor != null)
                    {
                        indexer = candidate;
                        return true;
                    }
                }
            }

            indexerAccessor = default;
            indexer = default;
            return false;
        }

        public override bool Writable => _indexer.CanWrite;

        protected override object? DoGetValue(object target)
        {
            if (_getter is null)
            {
                ExceptionHelper.ThrowInvalidOperationException("Indexer has no public getter.");
                return null;
            }

            object[] parameters = { _key };

            if (_containsKey != null)
            {
                if (_containsKey.Invoke(target, parameters) as bool? != true)
                {
                    return JsValue.Undefined;
                }
            }

            try
            {
                return _getter.Invoke(target, parameters);
            }
            catch (TargetInvocationException tie) when (tie.InnerException is KeyNotFoundException)
            {
                return JsValue.Undefined;
            }
        }

        protected override void DoSetValue(object target, object? value)
        {
            if (_setter is null)
            {
                ExceptionHelper.ThrowInvalidOperationException("Indexer has no public setter.");
            }

            object?[] parameters = { _key, value };
            _setter.Invoke(target, parameters);
        }

        public override PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target, bool enumerable = true)
        {
            if (_containsKey != null)
            {
                if (_containsKey.Invoke(target, new[] { _key }) as bool? != true)
                {
                    return PropertyDescriptor.Undefined;
                }
            }

            return new ReflectionDescriptor(engine, this, target, enumerable: true);
        }
    }
}
