using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native.Set
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-%setiteratorprototype%-object
    /// </summary>
    internal sealed class SetIteratorPrototype : IteratorPrototype
    {
        internal SetIteratorPrototype(
            Engine engine,
            Realm realm,
            ObjectPrototype objectPrototype) : base(engine, realm, "Set Iterator", objectPrototype)
        {
        }

        internal IteratorInstance ConstructEntryIterator(SetInstance set)
        {
            var instance = new SetEntryIterator(Engine, set)
            {
                _prototype = this
            };

            return instance;
        }

        internal IteratorInstance ConstructValueIterator(SetInstance set)
        {
            var instance = new IteratorInstance.ListIterator(Engine, set._set._list)
            {
                _prototype = this
            };

            return instance;
        }

        private sealed class SetEntryIterator : IteratorInstance
        {
            private readonly SetInstance _set;
            private int _position;

            public SetEntryIterator(Engine engine, SetInstance set) : base(engine)
            {
                _set = set;
                _position = 0;
            }

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                if (_position < _set._set._list.Count)
                {
                    var value = _set._set[_position];
                    _position++;
                    nextItem = new KeyValueIteratorPosition(_engine, value, value);
                    return true;
                }

                nextItem = KeyValueIteratorPosition.Done(_engine);
                return false;
            }
        }

    }
}