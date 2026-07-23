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

    /// <summary>
    /// An indexer is probed before the member itself (see <see cref="GetValue"/>), so the compiled
    /// lanes in <see cref="CompilableMemberAccessor"/> must decline when one is present.
    /// </summary>
    protected bool HasIndexer => _indexer is not null;

    protected abstract object? DoGetValue(object target, string memberName);

    protected abstract void DoSetValue(object target, string memberName, object? value);

    /// <summary>
    /// Compiled fast lane: reads the member and produces the <see cref="JsValue"/> directly,
    /// skipping the boxed round-trip through <see cref="JsValue.FromObjectWithType"/>. Returns
    /// <see langword="false"/> when this accessor has no such lane, leaving the caller to run
    /// <see cref="GetValue"/> plus its conversion.
    /// </summary>
    public virtual bool TryGetJsValue(Engine engine, object target, [NotNullWhen(true)] out JsValue? value)
    {
        value = null;
        return false;
    }

    /// <summary>
    /// Compiled fast lane: writes an exact-typed <see cref="JsValue"/> straight to the member.
    /// Returns <see langword="false"/> — having written nothing — when this accessor has no such
    /// lane or the value is not the exact expected JavaScript type, leaving the caller to run the
    /// full conversion path.
    /// </summary>
    public virtual bool TrySetJsValue(Engine engine, object target, JsValue value) => false;

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
                engine.CheckAmortizedConstraintsAtHostBoundary();
                Throw.MeaningfulException(engine, tie);
            }
        }

        // the success-path boundary check runs in ReflectionDescriptor.DoGet after result
        // conversion, so an awaitable value gets its continuation attached before a throw
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
        // compiled fast lane: an exact-typed value is written without boxing and without the
        // conversion dispatch below; anything else declines here having written nothing
        if (TrySetJsValue(engine, target, value))
        {
            engine.CheckAmortizedConstraintsAtHostBoundary();
            return;
        }

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
            engine.CheckAmortizedConstraintsAtHostBoundary();
            Throw.MeaningfulException(engine, exception);
        }

        engine.CheckAmortizedConstraintsAtHostBoundary();
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
