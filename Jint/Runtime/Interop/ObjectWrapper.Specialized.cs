using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Jint.Extensions;
using Jint.Native;

namespace Jint.Runtime.Interop;

internal abstract class ArrayLikeWrapper : ObjectWrapper
{
    protected ArrayLikeWrapper(
        Engine engine,
        object obj,
        Type itemType,
        Type? type = null) : base(engine, obj, type)
    {
        ItemType = itemType;
        if (engine.Options.Interop.AttachArrayPrototype)
        {
            Prototype = engine.Intrinsics.Array.PrototypeObject;
        }
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)]
    private Type ItemType { get; }

    public abstract int Length { get; }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        if (property.IsInteger())
        {
            return FromObject(_engine, GetAt(property.AsInteger()));
        }

        return base.Get(property, receiver);
    }

    public override bool HasProperty(JsValue property)
    {
        if (property.IsNumber())
        {
            var value = ((JsNumber) property)._value;
            if (TypeConverter.IsIntegralNumber(value))
            {
                var index = (int) value;
                if (Target is ICollection collection && index < collection.Count)
                {
                    return true;
                }
            }
        }

        return base.HasProperty(property);
    }

    public override bool Delete(JsValue property)
    {
        if (!_engine.Options.Interop.AllowWrite)
        {
            return false;
        }

        if (property.IsNumber())
        {
            var value = ((JsNumber) property)._value;
            if (TypeConverter.IsIntegralNumber(value))
            {
                DoSetAt((int) value, default);
                return true;
            }
        }

        return base.Delete(property);
    }


    public abstract object? GetAt(int index);

    public void SetAt(int index, JsValue value)
    {
        if (_engine.Options.Interop.AllowWrite)
        {
            EnsureCapacity(index + 1);
            DoSetAt(index, ConvertToItemType(value));
        }
    }

    protected abstract void DoSetAt(int index, object? value);

    public abstract void AddDefault();

    public abstract void Add(JsValue value);

    public abstract void RemoveAt(int index);

    public virtual void EnsureCapacity(int capacity)
    {
        while (Length < capacity)
        {
            AddDefault();
        }
    }

    protected object? ConvertToItemType(JsValue value)
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
}

internal class ListWrapper : ArrayLikeWrapper
{
    private readonly IList? _list;

    internal ListWrapper(Engine engine, IList target, Type type)
        : base(engine, target, typeof(object), type)
    {
        _list = target;
    }

    public override int Length => _list?.Count ?? 0;

    public override object? GetAt(int index)
    {
        if (_list is not null && index >= 0 && index < _list.Count)
        {
            return _list[index];
        }

        return null;
    }

    protected override void DoSetAt(int index, object? value)
    {
        if (_list is not null)
        {
            _list[index] = value;
        }
    }

    public override void AddDefault() => _list?.Add(null);

    public override void Add(JsValue value) => _list?.Add(ConvertToItemType(value));

    public override void RemoveAt(int index) => _list?.RemoveAt(index);
}

internal class GenericListWrapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] T> : ArrayLikeWrapper
{
    private readonly IList<T> _list;

    public GenericListWrapper(Engine engine, IList<T> target, Type? type)
        : base(engine, target, typeof(T), type)
    {
        _list = target;
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

    protected override void DoSetAt(int index, object? value) => _list[index] = (T) value!;

    public override void AddDefault() => _list.Add(default!);

    public override void Add(JsValue value)
    {
        var converted = ConvertToItemType(value);
        _list.Add((T) converted!);
    }

    public override void RemoveAt(int index) => _list.RemoveAt(index);
}

internal sealed class ReadOnlyListWrapper<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] T> : ArrayLikeWrapper
{
    private readonly IReadOnlyList<T> _list;

    public ReadOnlyListWrapper(Engine engine, IReadOnlyList<T> target, Type type) : base(engine, target, typeof(T), type)
    {
        _list = target;
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

    public override void AddDefault() => ExceptionHelper.ThrowNotSupportedException();

    protected override void DoSetAt(int index, object? value) => ExceptionHelper.ThrowNotSupportedException();

    public override void Add(JsValue value) => ExceptionHelper.ThrowNotSupportedException();

    public override void RemoveAt(int index) => ExceptionHelper.ThrowNotSupportedException();

    public override void EnsureCapacity(int capacity) => ExceptionHelper.ThrowNotSupportedException();
}
