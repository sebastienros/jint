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
[JsObject(UseShape = true)]
internal sealed partial class ArrayIteratorPrototype : IteratorPrototype
{
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)] private static readonly JsString ArrayIteratorToStringTag = new("Array Iterator");

    // Captured by CreateProperties_Generated (via CaptureField below) before any user code can
    // replace `next`; HasOriginalNext identity-compares against this snapshot to detect overrides.
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
    }

    [JsFunction(Name = "next", CaptureField = nameof(_originalNextFunction))]
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

        // for-of over a value-kind array iterator only needs the element, never the per-step
        // IteratorResult object TryIteratorStep allocates. Mirror that method exactly (live length
        // read each step, same done/closed transition, TryGetValue leaves holes as undefined) but
        // hand the value back directly. Key/Entry kinds keep the wrapping path via base.
        internal override bool TryStepValue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsValue? value)
        {
            if (_kind == ArrayIteratorType.Value)
            {
                var len = _array.GetLength();
                var position = _position;
                if (!_closed && position < len)
                {
                    _array.TryGetValue(position, out var stepped);
                    _position++;
                    value = stepped;
                    return true;
                }

                _closed = true;
                value = null;
                return false;
            }

            return base.TryStepValue(out value);
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

        // Value-kind for-of over a non-typed-array array-like only needs the element, so skip the
        // per-step IteratorResult TryIteratorStep allocates. Mirrors the non-typed-array branch of
        // that method exactly (live length each step, same done/closed transition, TryGetValue
        // leaves holes as undefined). Typed arrays keep their detach/out-of-bounds guarded path,
        // and Key/Entry kinds keep the wrapping path, both via base.
        internal override bool TryStepValue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsValue? value)
        {
            if (_kind == ArrayIteratorType.Value && _typedArray is null)
            {
                var len = _operations!.GetLength();
                if (!_closed && _position < len)
                {
                    _operations!.TryGetValue(_position, out var stepped);
                    _position++;
                    value = stepped;
                    return true;
                }

                _closed = true;
                value = null;
                return false;
            }

            return base.TryStepValue(out value);
        }
    }
}
