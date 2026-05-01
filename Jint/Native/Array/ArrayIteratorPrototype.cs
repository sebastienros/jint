using Jint.Native.ArrayBuffer;
using Jint.Native.Iterator;
using Jint.Native.Object;
using Jint.Native.TypedArray;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using FunctionInstance = Jint.Native.Function.Function;

namespace Jint.Native.Array;

/// <summary>
/// https://tc39.es/ecma262/#sec-%arrayiteratorprototype%-object
/// </summary>
[JsObject]
internal sealed partial class ArrayIteratorPrototype : IteratorPrototype
{
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString ArrayIteratorToStringTag = new("Array Iterator");

    // Captured once Initialize runs; HasOriginalNext below identity-compares against this snapshot
    // to detect whether user code has overridden `next` on the prototype.
    private FunctionInstance _originalNextFunction = null!;

    internal ArrayIteratorPrototype(
        Engine engine,
        Realm realm,
        IteratorPrototype objectPrototype) : base(engine, realm, objectPrototype)
    {
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();

        // Snapshot the prototype's `next` function before any user code can replace it,
        // so HasOriginalNext can detect overrides via ReferenceEquals.
        _originalNextFunction = (FunctionInstance) GetOwnProperty("next").Value!;
    }

    [JsFunction(Length = 0, Name = "next")]
    private JsValue NextHandler(JsValue thisObject) => Next(thisObject, Arguments.Empty);

    internal IteratorInstance Construct(ObjectInstance array, ArrayIteratorType kind)
    {
        if (!HasOriginalNext)
        {
            return new IteratorInstance.ObjectIterator(this);
        }

        IteratorInstance instance = array is JsArray jsArray
            ? new ArrayIterator(Engine, jsArray, kind) { _prototype = this }
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
                    Throw.TypeError(_typedArray.Engine.Realm, "TypedArray is out of bounds");
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
