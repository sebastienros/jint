using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Jint.Extensions;
using Jint.Native;
#if NET8_0_OR_GREATER
using System.Text.Json.Nodes;
#endif
using Jint.Native.Object;

namespace Jint.Runtime.Interop
{
    public class ArrayLikeWrapper : ObjectInstance, IObjectWrapper
    {
        private static readonly Type _genericListType = typeof(IList<>);
        private static readonly Type _genericReadonlyListType = typeof(IReadOnlyList<>);

        private readonly ICollection? _collection;
        private readonly IList? _list;

        internal ArrayLikeWrapper(Engine engine, object? target)
            : base(engine)
        {
            if (target == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(target));
            }

            Target = target;
            ItemType = typeof(object);
            _collection = target as ICollection;
            _list = target as IList;
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }
    
        [RequiresDynamicCode("The target type is not known at compile time.")]
        public static ArrayLikeWrapper Create(Engine engine, object? target)
        {
            if (target == null)
            {
                ExceptionHelper.ThrowArgumentNullException(nameof(target));
            }

            if (target is IList list)
                return new ArrayLikeWrapper(engine, list);

            if (target is IList<object> objectList)
                return new ArrayLikeWrapper<object>(engine, objectList);

#if NET8_0_OR_GREATER
            if (target is JsonArray array)
                return new ArrayLikeWrapper<JsonNode?>(engine, array);
#endif

            var type = target.GetType();
            if (type.IsGenericType && type.GetGenericTypeDefinition() == _genericListType)
            {
                var arrayItemType = type.GenericTypeArguments[0];
#pragma warning disable IL2055
                var arrayWrapperType = typeof(ArrayLikeWrapper<>).MakeGenericType(arrayItemType);
#pragma warning restore IL2055
                return (ArrayLikeWrapper) Activator.CreateInstance(arrayWrapperType, engine, target)!;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == _genericReadonlyListType)
            {
                var arrayItemType = type.GenericTypeArguments[0];
#pragma warning disable IL2055
                var arrayWrapperType = typeof(ReadOnlyArrayLikeWrapper<>).MakeGenericType(arrayItemType);
#pragma warning restore IL2055
                return (ArrayLikeWrapper) Activator.CreateInstance(arrayWrapperType, engine, target)!;
            }

            return new ArrayLikeWrapper(engine, target);
        }

        public object Target { get; }
        
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)]
        public Type ItemType { get; internal set; }
        public virtual int Length => _collection?.Count ?? 0;
        
        internal override bool IsArrayLike => true;
        internal override bool HasOriginalIterator => true;
        internal override bool IsIntegerIndexedArray => true;
        
        public object? ConvertToItemType(JsValue value)
        {
            object? converted;
            if (ItemType == typeof(JsValue))
            {
                converted = value;
            }
            else if (!ReflectionExtensions.TryConvertViaTypeCoercion(ItemType, Engine.Options.Interop.ValueCoercion, value, out converted))
            {
                // attempt to convert the JsValue to the target type
                converted = value.ToObject();
                if (converted != null && converted.GetType() != ItemType)
                {
                    converted = Engine.TypeConverter.Convert(converted, ItemType, CultureInfo.InvariantCulture);
                }
            }
            
            return converted;
        }
        
        public virtual object? GetAt(int index)
        {
            if (_list is not null && index >= 0 && index < _list.Count)
            {
                return _list[index];
            }

            return null;
        }

        public virtual void SetAt(int index, JsValue value)
        {
            EnsureCapacity(index);

            if (_list is not null)
            {
                _list[index] = ConvertToItemType(value);
            }
        }
        
        public virtual void AddDefault()
        {
            _list?.Add(null);
        }
        
        public virtual void Add(JsValue value)
        {
            _list?.Add(ConvertToItemType(value));
        }

        public virtual void RemoveAt(int index)
        {
            _list?.RemoveAt(index);
        }

        public virtual void EnsureCapacity(int capacity)
        {
            if (_list is null)
            {
                return;
            }

            while (_list.Count < capacity)
            {
                AddDefault();
            }
        }

        public override object ToObject()
        {
            return Target;
        }
        
        internal override ulong GetSmallestIndex(ulong length)
        {
            return Target is ICollection ? 0 : base.GetSmallestIndex(length);
        }
        
        public override bool Equals(object? obj) => Equals(obj as ObjectWrapper);
        
        public override bool Equals(JsValue? other) => Equals(other as ObjectWrapper);
        
        public bool Equals(ObjectWrapper? other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(Target, other.Target);
        }
        
        public override int GetHashCode() => Target.GetHashCode();
    }

    public class ArrayLikeWrapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)]T> : ArrayLikeWrapper
    {
        private readonly IList<T> _list;

        public ArrayLikeWrapper(Engine engine, IList<T> target)
            : base(engine, target)
        {
            _list = target;
            ItemType = typeof(T);
        }

        public override int Length => _list.Count;

        public override object? GetAt(int index)
        {
            if (index >= 0 && index < _list.Count)
            {
                return _list[index];
            }

            return null;
        }

        public override void SetAt(int index, JsValue value)
        {
            EnsureCapacity(index);
            var converted = ConvertToItemType(value);
            _list[index] = (T)converted!;
        }
        
        public override void AddDefault()
        {
            _list.Add(default!);
        }
        
        public override void Add(JsValue value)
        {
            var converted = ConvertToItemType(value);
            _list.Add((T)converted!);
        }

        public override void RemoveAt(int index)
        {
            _list.RemoveAt(index);
        }

        public override void EnsureCapacity(int capacity)
        {
            while (_list.Count < capacity)
            {
                AddDefault();
            }
        }
    }

    public class ReadOnlyArrayLikeWrapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)]T> : ArrayLikeWrapper
    {
        private readonly IReadOnlyList<T> _list;

        public ReadOnlyArrayLikeWrapper(Engine engine, IReadOnlyList<T> target)
            : base(engine, target)
        {
            _list = target;
            ItemType = typeof(T);
        }

        public override int Length => _list.Count;

        public override object? GetAt(int index)
        {
            if (index >= 0 && index < _list.Count)
            {
                return _list[index];
            }

            return null;
        }

        public override void AddDefault()
        {
            ExceptionHelper.ThrowNotSupportedException();
        }

        public override void SetAt(int index, JsValue value)
        {
            ExceptionHelper.ThrowNotSupportedException();
        }

        public override void Add(JsValue value)
        {
            ExceptionHelper.ThrowNotSupportedException();
        }

        public override void RemoveAt(int index)
        {
            ExceptionHelper.ThrowNotSupportedException();
        }

        public override void EnsureCapacity(int capacity)
        {
            ExceptionHelper.ThrowNotSupportedException();
        }
    }
}
