using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using Jint.Extensions;
using Jint.Native;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

#pragma warning disable IL2098
#pragma warning disable IL2072
#pragma warning disable IL2077

namespace Jint.Runtime.Interop.Reflection;

/// <summary>
/// Strategy to read and write CLR object properties and fields.
/// </summary>
internal abstract class ReflectionAccessor
{
    private readonly Type? _memberType;
    private readonly PropertyInfo? _indexer;

    public Type? MemberType => _memberType;

    protected ReflectionAccessor(
        Type? memberType,
        PropertyInfo? indexer = null)
    {
        _memberType = memberType;
        _indexer = indexer;
    }

    public virtual bool Readable => true;

    public abstract bool Writable { get; }

    protected abstract object? DoGetValue(object target, string memberName);

    protected abstract void DoSetValue(object target, string memberName, object? value);

    public object? GetValue(Engine engine, object target, string memberName)
    {
        var constantValue = ConstantValue;
        if (constantValue is not null)
        {
            return constantValue;
        }

        // first check indexer so we don't confuse inherited properties etc
        var value = TryReadFromIndexer(target, memberName);

        if (value is null)
        {
            try
            {
                value = DoGetValue(target, memberName);
            }
            catch (TargetInvocationException tie)
            {
                ExceptionHelper.ThrowMeaningfulException(engine, tie);
            }
        }

        return value;
    }

    protected virtual JsValue? ConstantValue => null;

    private object? TryReadFromIndexer(object target, string memberName)
    {
        var getter = _indexer?.GetGetMethod();
        if (getter is null)
        {
            return null;
        }

        try
        {
            object[] parameters = [memberName];
            return getter.Invoke(target, parameters);
        }
        catch
        {
            return null;
        }
    }

    public void SetValue(Engine engine, object target, string memberName, JsValue value)
    {
        object? converted;
        if (_memberType == typeof(JsValue))
        {
            converted = value;
        }
        else if (!ReflectionExtensions.TryConvertViaTypeCoercion(_memberType, engine.Options.Interop.ValueCoercion, value, out converted))
        {
            // attempt to convert the JsValue to the target type
            converted = value.ToObject();
            if (converted != null && converted.GetType() != _memberType)
            {
                converted = ConvertValueToSet(engine, converted);
            }
        }

        try
        {
            DoSetValue(target, memberName, converted);
        }
        catch (TargetInvocationException exception)
        {
            ExceptionHelper.ThrowMeaningfulException(engine, exception);
        }
    }

    protected virtual object? ConvertValueToSet(Engine engine, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields)] object value)
    {
        var memberType = _memberType ?? value.GetType();
        return engine.TypeConverter.Convert(value, memberType, CultureInfo.InvariantCulture);
    }

    public virtual PropertyDescriptor CreatePropertyDescriptor(Engine engine, object target, string memberName, bool enumerable = true)
    {
        return new ReflectionDescriptor(engine, this, target, memberName, enumerable);
    }
}
