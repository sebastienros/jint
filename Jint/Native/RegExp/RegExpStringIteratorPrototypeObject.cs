using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.RegExp
{
    internal sealed class RegExpStringIteratorPrototypeObject : IteratorPrototype
    {
        internal RegExpStringIteratorPrototypeObject(
            Engine engine,
            Realm realm,
            ObjectPrototype objectPrototype) : base(engine, realm, "RegExp String Iterator", objectPrototype)
        {
        }

        internal IteratorInstance Construct(ObjectInstance iteratingRegExp, string iteratedString, bool global, bool unicode)
        {
            var instance = new IteratorInstance.RegExpStringIterator(Engine, iteratingRegExp, iteratedString, global, unicode)
            {
                _prototype = this
            };

            return instance;
        }
    }
}