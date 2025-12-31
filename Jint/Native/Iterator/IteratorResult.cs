using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-createiterresultobject
/// </summary>
internal sealed class IteratorResult : ObjectInstance
{
    // Store as PropertyDescriptor fields for fast access while supporting delete
    // Null means the property has been deleted
    private PropertyDescriptor? _valueDesc;
    private PropertyDescriptor? _doneDesc;

    public IteratorResult(Engine engine, JsValue value, JsBoolean done) : base(engine)
    {
        _valueDesc = new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable);
        _doneDesc = new PropertyDescriptor(done, PropertyFlag.ConfigurableEnumerableWritable);
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
            return _valueDesc?.Value ?? base.Get(property, receiver);
        }

        if (CommonProperties.Done.Equals(property))
        {
            return _doneDesc?.Value ?? base.Get(property, receiver);
        }

        return base.Get(property, receiver);
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (CommonProperties.Value.Equals(property))
        {
            return _valueDesc ?? base.GetOwnProperty(property);
        }

        if (CommonProperties.Done.Equals(property))
        {
            return _doneDesc ?? base.GetOwnProperty(property);
        }

        return base.GetOwnProperty(property);
    }

    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        if (CommonProperties.Value.Equals(property) && _valueDesc is not null)
        {
            _valueDesc.Value = value;
            return true;
        }

        if (CommonProperties.Done.Equals(property) && _doneDesc is not null)
        {
            _doneDesc.Value = value;
            return true;
        }

        return base.Set(property, value, receiver);
    }

    protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (CommonProperties.Value.Equals(property))
        {
            _valueDesc = desc;
            return;
        }

        if (CommonProperties.Done.Equals(property))
        {
            _doneDesc = desc;
            return;
        }

        base.SetOwnProperty(property, desc);
    }

    public override void RemoveOwnProperty(JsValue property)
    {
        if (CommonProperties.Value.Equals(property))
        {
            _valueDesc = null;
            return;
        }

        if (CommonProperties.Done.Equals(property))
        {
            _doneDesc = null;
            return;
        }

        base.RemoveOwnProperty(property);
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        var keys = new List<JsValue>();

        if ((types & Types.String) != Types.Empty)
        {
            if (_valueDesc is not null)
            {
                keys.Add(CommonProperties.Value);
            }
            if (_doneDesc is not null)
            {
                keys.Add(CommonProperties.Done);
            }
            keys.AddRange(base.GetOwnPropertyKeys(Types.String));
        }

        if ((types & Types.Symbol) != Types.Empty)
        {
            keys.AddRange(base.GetOwnPropertyKeys(Types.Symbol));
        }

        return keys;
    }

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        if (_valueDesc is not null)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Value, _valueDesc);
        }

        if (_doneDesc is not null)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Done, _doneDesc);
        }

        foreach (var entry in base.GetOwnProperties())
        {
            yield return entry;
        }
    }

    public override object ToObject() => this;
}
