using Jint.Native.Object;
using Jint.Runtime;

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
        var array = done ? Undefined : new JsArray(engine, new[] { key!, value! });

        return new IteratorResult(engine, array, JsBoolean.Create(done));
    }

    public override JsValue Get(JsValue property, JsValue receiver)
    {
        if (property == CommonProperties.Value)
        {
            return _value;
        }

        if (property == CommonProperties.Done)
        {
            return _done;
        }

        return base.Get(property, receiver);
    }

    public override object ToObject() => this;
}
