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
