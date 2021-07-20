using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.RegExp
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-%regexpstringiteratorprototype%-object
    /// </summary>
    internal sealed class RegExpStringIteratorPrototype : IteratorPrototype
    {
        internal RegExpStringIteratorPrototype(
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