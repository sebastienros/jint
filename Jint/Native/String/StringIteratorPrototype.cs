using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.String
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-%stringiteratorprototype%-object
    /// </summary>
    internal sealed class StringIteratorPrototype : IteratorPrototype
    {
        internal StringIteratorPrototype(
            Engine engine,
            Realm realm,
            ObjectPrototype objectPrototype) : base(engine, realm, "String Iterator", objectPrototype)
        {
        }

        public ObjectInstance Construct(string str)
        {
            var instance = new IteratorInstance.StringIterator(Engine, str)
            {
                _prototype = this
            };

            return instance;
        }
    }
}