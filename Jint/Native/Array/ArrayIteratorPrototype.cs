using Jint.Collections;
using Jint.Native.ArrayBuffer;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Array;

/// <summary>
/// https://tc39.es/ecma262/#sec-%arrayiteratorprototype%-object
/// </summary>
internal sealed class ArrayIteratorPrototype : IteratorPrototype
{
    private ClrFunction? _originalNextFunction;

    internal ArrayIteratorPrototype(
        Engine engine,
        Realm realm,
        IteratorPrototype objectPrototype) : base(engine, realm, objectPrototype)
    {
    }

    protected override void Initialize()
    {
        _originalNextFunction = new ClrFunction(Engine, "next", Next, 0, PropertyFlag.Configurable);
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            [KnownKeys.Next] = new(_originalNextFunction, PropertyFlag.NonEnumerable)
        };
        SetProperties(properties);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Array Iterator", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    internal IteratorInstance Construct(ObjectInstance array, ArrayIteratorType kind)
    {
        if (!HasOriginalNext)
        {
            return new IteratorInstance.ObjectIterator(this);
        }

        IteratorInstance instance = array is JsArray jsArray
            ? new ArrayIterator(Engine, jsArray, kind)  { _prototype = this }
            : new ArrayLikeIterator(Engine, array, kind) { _prototype = this };

        return instance;
    }

    internal bool HasOriginalNext
        => ReferenceEquals(Get(CommonProperties.Next), _originalNextFunction);

    private sealed class ArrayIterator : IteratorInstance
    {
        private readonly ArrayIteratorType _kind;
        private readonly JsArray _array;
        private uint _position;
        private bool _closed;

        public ArrayIterator(Engine engine, JsArray array, ArrayIteratorType kind) : base(engine)
        {
            _kind = kind;
            _array = array;
            _position = 0;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            var len = _array.GetLength();
            var position = _position;
            if (!_closed && position < len)
            {
                _array.TryGetValue(position, out var value);
                nextItem = _kind switch
                {
                    ArrayIteratorType.Key => IteratorResult.CreateValueIteratorPosition(_engine, JsNumber.Create(position)),
                    ArrayIteratorType.Value => IteratorResult.CreateValueIteratorPosition(_engine, value),
                    _ => IteratorResult.CreateKeyValueIteratorPosition(_engine, JsNumber.Create(position), value)
                };

                _position++;
                return true;
            }

            _closed = true;
            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }

    private sealed class ArrayLikeIterator : IteratorInstance
    {
        private readonly ArrayIteratorType _kind;
        private readonly JsTypedArray? _typedArray;
        private readonly ArrayOperations? _operations;
        private uint _position;
        private bool _closed;

        public ArrayLikeIterator(Engine engine, ObjectInstance objectInstance, ArrayIteratorType kind) : base(engine)
        {
            _kind = kind;
            _typedArray = objectInstance as JsTypedArray;
            if (_typedArray is null)
            {
                _operations = ArrayOperations.For(objectInstance, forWrite: false);
            }

            _position = 0;
        }

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            uint len;
            if (_typedArray is not null)
            {
                _typedArray._viewedArrayBuffer.AssertNotDetached();
                var taRecord = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(_typedArray, ArrayBufferOrder.SeqCst);
                if (!_closed && taRecord.IsTypedArrayOutOfBounds)
                {
                    ExceptionHelper.ThrowTypeError(_typedArray.Engine.Realm, "TypedArray is out of bounds");
                }
                len = taRecord.TypedArrayLength;
            }
            else
            {
                len = _operations!.GetLength();
            }

            if (!_closed && _position < len)
            {
                if (_typedArray is not null)
                {
                    nextItem = _kind switch
                    {
                        ArrayIteratorType.Key => IteratorResult.CreateValueIteratorPosition(_engine, JsNumber.Create(_position)),
                        ArrayIteratorType.Value => IteratorResult.CreateValueIteratorPosition(_engine, _typedArray[(int) _position]),
                        _ => IteratorResult.CreateKeyValueIteratorPosition(_engine, JsNumber.Create(_position), _typedArray[(int) _position])
                    };
                }
                else
                {
                    _operations!.TryGetValue(_position, out var value);
                    nextItem = _kind switch
                    {
                        ArrayIteratorType.Key => IteratorResult.CreateValueIteratorPosition(_engine, JsNumber.Create(_position)),
                        ArrayIteratorType.Value => IteratorResult.CreateValueIteratorPosition(_engine, value),
                        _ => IteratorResult.CreateKeyValueIteratorPosition(_engine, JsNumber.Create(_position), value)
                    };
                }

                _position++;
                return true;
            }

            _closed = true;
            nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
            return false;
        }
    }
}
