using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-createiterresultobject
/// </summary>
internal sealed class IteratorResult : ObjectInstance
{
    // value/done are stored inline so a per-step result object allocates NO PropertyDescriptor on the hot
    // iteration path: for-of / spread / destructuring / generators read them via Get(), served straight from
    // these fields, and the result object is then discarded. A heap PropertyDescriptor is materialized (and
    // cached, so [[DefineOwnProperty]] mutations persist) only on the cold reflective paths
    // (GetOwnProperty / GetOwnProperties), or stored directly when a script redefines the property via
    // SetOwnProperty — in which case the descriptor takes precedence over the inline value. A null inline
    // value together with a null descriptor means the property has been deleted.
    private JsValue? _value;
    private JsValue? _done;
    private PropertyDescriptor? _valueDesc;
    private PropertyDescriptor? _doneDesc;

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
            if (_valueDesc is not null)
            {
                return _valueDesc.Value;
            }
            return _value ?? base.Get(property, receiver);
        }

        if (CommonProperties.Done.Equals(property))
        {
            if (_doneDesc is not null)
            {
                return _doneDesc.Value;
            }
            return _done ?? base.Get(property, receiver);
        }

        return base.Get(property, receiver);
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (CommonProperties.Value.Equals(property))
        {
            // Materialize lazily and cache: callers (e.g. ValidateAndApplyPropertyDescriptor) mutate the
            // returned descriptor in place and expect the change to land in storage.
            if (_valueDesc is null && _value is not null)
            {
                _valueDesc = new PropertyDescriptor(_value, PropertyFlag.ConfigurableEnumerableWritable);
            }
            return _valueDesc ?? base.GetOwnProperty(property);
        }

        if (CommonProperties.Done.Equals(property))
        {
            if (_doneDesc is null && _done is not null)
            {
                _doneDesc = new PropertyDescriptor(_done, PropertyFlag.ConfigurableEnumerableWritable);
            }
            return _doneDesc ?? base.GetOwnProperty(property);
        }

        return base.GetOwnProperty(property);
    }

    public override bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        if (CommonProperties.Value.Equals(property))
        {
            if (_valueDesc is not null)
            {
                _valueDesc.Value = value;
                return true;
            }
            if (_value is not null)
            {
                _value = value;
                return true;
            }
        }
        else if (CommonProperties.Done.Equals(property))
        {
            if (_doneDesc is not null)
            {
                _doneDesc.Value = value;
                return true;
            }
            if (_done is not null)
            {
                _done = value;
                return true;
            }
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
            _value = null;
            _valueDesc = null;
            return;
        }

        if (CommonProperties.Done.Equals(property))
        {
            _done = null;
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
            if (_value is not null || _valueDesc is not null)
            {
                keys.Add(CommonProperties.Value);
            }
            if (_done is not null || _doneDesc is not null)
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
        if (_value is not null || _valueDesc is not null)
        {
            _valueDesc ??= new PropertyDescriptor(_value!, PropertyFlag.ConfigurableEnumerableWritable);
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Value, _valueDesc);
        }

        if (_done is not null || _doneDesc is not null)
        {
            _doneDesc ??= new PropertyDescriptor(_done!, PropertyFlag.ConfigurableEnumerableWritable);
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(CommonProperties.Done, _doneDesc);
        }

        foreach (var entry in base.GetOwnProperties())
        {
            yield return entry;
        }
    }

    public override object ToObject() => this;
}
