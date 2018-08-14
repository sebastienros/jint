using System.Collections.Generic;
using System.Linq;
using Jint.Native.Object;

namespace Jint.Native.Iterator
{
    public class IteratorInstance : ObjectInstance
    {
        private readonly IEnumerator<JsValue> _enumerable;

        public IteratorInstance(Engine engine)
            : this(engine, Enumerable.Empty<JsValue>())
        {
        }

        public IteratorInstance(
            Engine engine,
            IEnumerable<JsValue> enumerable) : base(engine, "Iterator")
        {
            _enumerable = enumerable.GetEnumerator();
        }

        public override object ToObject()
        {
            throw new System.NotImplementedException();
        }

        public override bool Equals(JsValue other)
        {
            return false;
        }

        public JsValue Next()
        {
            if (_enumerable.MoveNext())
            {
                return new IteratorPosition(_engine, 0, 0);
            }

            return IteratorPosition.Done;
        }

        public class IteratorPosition : ObjectInstance
        {
            internal static JsValue Done = new IteratorPosition(null, null, null);

            public IteratorPosition(Engine engine, object key, object value) : base(engine)
            {

            }
        }
    }
}