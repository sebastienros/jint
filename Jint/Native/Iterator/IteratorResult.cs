using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Iterator
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-createiterresultobject
    /// </summary>
    internal class IteratorResult : ObjectInstance
    {
        private readonly JsValue _value;
        private readonly JsBoolean _done;

        public IteratorResult(Engine engine, JsValue value, JsBoolean done) : base(engine)
        {
            _value = value;
            _done = done;
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

        public override object ToObject()
        {
            return this;
        }

        public override bool Equals(JsValue other)
        {
            return ReferenceEquals(this, other);
        }
    }
}