using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;

namespace Jint.Native.Array
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-%arrayiteratorprototype%-object
    /// </summary>
    internal sealed class ArrayIteratorPrototype : IteratorPrototype
    {
        internal ArrayIteratorPrototype(
            Engine engine,
            Realm realm,
            ObjectPrototype objectPrototype) : base(engine, realm, "Array Iterator", objectPrototype)
        {
        }

        internal IteratorInstance Construct(ObjectInstance array, ArrayIteratorType kind)
        {
            var instance = new ArrayLikeIterator(Engine, array, kind)
            {
                _prototype = this
            };

            return instance;
        }

        private sealed class ArrayLikeIterator : IteratorInstance
        {
            private readonly ArrayIteratorType _kind;
            private readonly TypedArrayInstance _typedArray;
            private readonly ArrayOperations _operations;
            private uint _position;
            private bool _closed;

            public ArrayLikeIterator(Engine engine, ObjectInstance objectInstance, ArrayIteratorType kind) : base(engine)
            {
                _kind = kind;
                _typedArray = objectInstance as TypedArrayInstance;
                if (_typedArray is null)
                {
                    _operations = ArrayOperations.For(objectInstance);
                }

                _position = 0;
            }

            public override bool TryIteratorStep(out ObjectInstance nextItem)
            {
                uint len;
                if (_typedArray is not null)
                {
                    _typedArray._viewedArrayBuffer.AssertNotDetached();
                    len = _typedArray.Length;
                }
                else
                {
                    len = _operations.GetLength();
                }

                if (!_closed && _position < len)
                {
                    JsValue value;
                    if (_typedArray is not null)
                    {
                        nextItem = _kind switch
                        {
                            ArrayIteratorType.Key => new ValueIteratorPosition(_engine, _position),
                            ArrayIteratorType.Value => new ValueIteratorPosition(_engine, _typedArray[(int) _position]),
                            _ => new KeyValueIteratorPosition(_engine, _position, _typedArray[(int) _position])
                        };
                    }
                    else
                    {
                        _operations.TryGetValue(_position, out value);
                        if (_kind == ArrayIteratorType.Key)
                        {
                            nextItem = new ValueIteratorPosition(_engine, _position);
                        }
                        else if (_kind == ArrayIteratorType.Value)
                        {
                            nextItem = new ValueIteratorPosition(_engine, value);
                        }
                        else
                        {
                            nextItem = new KeyValueIteratorPosition(_engine, _position, value);
                        }
                    }

                    _position++;
                    return true;
                }

                _closed = true;
                nextItem = KeyValueIteratorPosition.Done(_engine);
                return false;
            }
        }
    }
}