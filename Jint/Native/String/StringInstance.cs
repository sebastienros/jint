using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.String;

internal class StringInstance : ObjectInstance, IJsPrimitive
{
    internal PropertyDescriptor? _length;

    public StringInstance(Engine engine, JsString value)
        : base(engine, ObjectClass.String)
    {
        StringData = value;
        _length = PropertyDescriptor.AllForbiddenDescriptor.ForNumber(value.Length);
    }

    Types IJsPrimitive.Type => Types.String;

    JsValue IJsPrimitive.PrimitiveValue => StringData;

    public JsString StringData { get; }

    private static bool IsInt32(double d, out int intValue)
    {
        if (d >= int.MinValue && d <= int.MaxValue)
        {
            intValue = (int) d;
            return intValue == d;
        }

        intValue = 0;
        return false;
    }

    public sealed override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (CommonProperties.Infinity.Equals(property))
        {
            return PropertyDescriptor.Undefined;
        }

        if (CommonProperties.Length.Equals(property))
        {
            return _length ?? PropertyDescriptor.Undefined;
        }

        var desc = base.GetOwnProperty(property);
        if (desc != PropertyDescriptor.Undefined)
        {
            return desc;
        }

        if ((property._type & (InternalTypes.Number | InternalTypes.Integer | InternalTypes.String)) == InternalTypes.Empty)
        {
            return PropertyDescriptor.Undefined;
        }

        var str = StringData.ToString();
        var number = TypeConverter.ToNumber(property);
        if (!IsInt32(number, out var index) || index < 0 || index >= str.Length)
        {
            return PropertyDescriptor.Undefined;
        }

        return new PropertyDescriptor(str[index], PropertyFlag.OnlyEnumerable);
    }

    /// <summary>
    /// Index and <c>length</c> existence answered from the string itself, without the
    /// <see cref="PropertyDescriptor"/> (and the boxed character) that
    /// <see cref="GetOwnProperty(JsValue)"/> allocates per index. Enumerating a String object
    /// (for-in, Object.keys/values/entries, Object.assign, spread, JSON.stringify) previously built one
    /// descriptor per character purely to test it; the probe also skips the
    /// <see cref="JsString.ToString"/> flattening a rope-backed value would otherwise pay.
    /// </summary>
    /// <remarks>
    /// Mirrors <see cref="GetOwnProperty(JsValue)"/> step for step, so the two agree at every instant:
    /// the <c>Infinity</c> guard, then <c>length</c> off the same <see cref="_length"/> field (whose
    /// flags a redefinition can change, so enumerability is read off the descriptor rather than assumed),
    /// then the ordinary property bag — which shadows an index, exactly as there — and only then the
    /// index lane, which yields <see cref="PropertyFlag.OnlyEnumerable"/> for an in-range index.
    /// </remarks>
    protected internal sealed override OwnPropertyProbe ProbeOwnProperty(JsValue property)
    {
        if (CommonProperties.Infinity.Equals(property))
        {
            return OwnPropertyProbe.Missing;
        }

        if (CommonProperties.Length.Equals(property))
        {
            var length = _length;
            if (length is null)
            {
                return OwnPropertyProbe.Missing;
            }

            return length.Enumerable ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.NonEnumerable;
        }

        // ObjectInstance.GetOwnProperty, called non-virtually: the ordinary bag lookup this type's
        // GetOwnProperty performs at this point, which allocates nothing for a String object.
        var desc = base.GetOwnProperty(property);
        if (!ReferenceEquals(desc, PropertyDescriptor.Undefined))
        {
            return desc.Enumerable ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.NonEnumerable;
        }

        if ((property._type & (InternalTypes.Number | InternalTypes.Integer | InternalTypes.String)) == InternalTypes.Empty)
        {
            return OwnPropertyProbe.Missing;
        }

        var number = TypeConverter.ToNumber(property);
        if (!IsInt32(number, out var index) || index < 0 || index >= StringData.Length)
        {
            return OwnPropertyProbe.Missing;
        }

        return OwnPropertyProbe.Enumerable;
    }

    public sealed override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        foreach (var entry in base.GetOwnProperties())
        {
            yield return entry;
        }

        if (_length != null)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Length, _length);
        }
    }

    internal sealed override IEnumerable<JsValue> GetInitialOwnStringPropertyKeys()
    {
        yield return JsString.LengthString;
    }

    public sealed override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        var keys = new List<JsValue>(StringData.Length + 1);
        if ((types & Types.String) != Types.Empty)
        {
            for (uint i = 0; i < StringData.Length; ++i)
            {
                keys.Add(JsString.Create(i));
            }

            keys.AddRange(base.GetOwnPropertyKeys(Types.String));
        }

        if ((types & Types.Symbol) != Types.Empty)
        {
            keys.AddRange(base.GetOwnPropertyKeys(Types.Symbol));
        }

        return keys;
    }

    protected internal sealed override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (CommonProperties.Length.Equals(property))
        {
            _length = desc;
        }
        else
        {
            base.SetOwnProperty(property, desc);
        }
    }

    public sealed override void RemoveOwnProperty(JsValue property)
    {
        if (CommonProperties.Length.Equals(property))
        {
            _length = null;
        }

        base.RemoveOwnProperty(property);
    }
}
