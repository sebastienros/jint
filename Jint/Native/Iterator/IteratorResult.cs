using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-createiterresultobject
/// </summary>
internal sealed class IteratorResult : ObjectInstance
{
    private readonly JsValue _value;
    private readonly JsBoolean _done;

    public IteratorResult(Engine engine, JsValue value, JsBoolean done) : base(engine)
    {
        _value = value;
        _done = done;
    }

    public static IteratorResult CreateValueIteratorPosition(Engine engine, JsValue? value = null, JsBoolean? done = null)
    {
        return new IteratorResult(engine, value ?? Undefined, done ?? JsBoolean.False);
    }

    public static IteratorResult CreateKeyValueIteratorPosition(Engine engine, JsValue? key = null, JsValue? value = null)
    {
        var done = key is null && value is null;
        var array = done ? Undefined : new JsArray(engine, [key!, value!]);

        return new IteratorResult(engine, array, JsBoolean.Create(done));
    }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        if (CommonProperties.Value.Equals(property))
        {
            return _value;
        }

        if (CommonProperties.Done.Equals(property))
        {
            return _done;
        }

        return base.Get(property, receiver);
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (CommonProperties.Value.Equals(property))
        {
            return new PropertyDescriptor(_value, PropertyFlag.ConfigurableEnumerableWritable);
        }

        if (CommonProperties.Done.Equals(property))
        {
            return new PropertyDescriptor(_done, PropertyFlag.ConfigurableEnumerableWritable);
        }

        return base.GetOwnProperty(property);
    }

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Value, new PropertyDescriptor(_value, PropertyFlag.ConfigurableEnumerableWritable));
        yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Done, new PropertyDescriptor(_done, PropertyFlag.ConfigurableEnumerableWritable));
        foreach (var prop in base.GetOwnProperties())
        {
            yield return prop;
        }
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        var keys = new List<JsValue>();
        if ((types & Types.String) != Types.Empty)
        {
            keys.Add(CommonProperties.Value);
            keys.Add(CommonProperties.Done);
        }
        keys.AddRange(base.GetOwnPropertyKeys(types));
        return keys;
    }

    public override object ToObject() => this;
}
