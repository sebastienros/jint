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
    // replace `next`; StepsNatively identity-compares against this snapshot to detect overrides.
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

    /// <summary>
    /// CreateArrayIterator (https://tc39.es/ecma262/#sec-createarrayiterator). The object handed back
    /// is script-visible — it is what <c>[][Symbol.iterator]()</c> evaluates to — so it always
    /// iterates <paramref name="array"/> with <paramref name="kind"/>, whatever <c>next</c> currently
    /// resolves to. Whether a *consumer* may then step it natively instead of calling <c>next</c> is
    /// decided where the iterator record is built, by <see cref="IteratorInstance.HasNativeNext"/>.
    /// </summary>
    internal IteratorInstance Construct(ObjectInstance array, ArrayIteratorType kind)
    {
        IteratorInstance instance = array is JsArray jsArray
            ? new ArrayIterator(Engine, jsArray, kind) { _prototype = this }
            : new ArrayLikeIterator(Engine, array, kind) { _prototype = this };

        return instance;
    }

    /// <summary>
    /// One read covers both ways an array iterator's <c>next</c> can stop being the built-in one: an
    /// own <c>next</c> on the instance shadowing the prototype's, and the prototype's own <c>next</c>
    /// having been replaced. A prototype swapped out from under the instance fails the type test and
    /// is treated as replaced too.
    /// </summary>
    private bool StepsNatively(ObjectInstance iterator)
        => ReferenceEquals(iterator.Get(CommonProperties.Next), _originalNextFunction);

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

        internal override bool HasNativeNext
            => _prototype is ArrayIteratorPrototype prototype && prototype.StepsNatively(this);

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
        private ulong _position;
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

        internal override bool HasNativeNext
            => _prototype is ArrayIteratorPrototype prototype && prototype.StepsNatively(this);

        public override bool TryIteratorStep(out ObjectInstance nextItem)
        {
            // https://tc39.es/ecma262/#sec-createarrayiterator - the abstract closure is a generator
            // body, so once it has returned the generator is completed and a further `next` answers
            // { value: undefined, done: true } without running a single step again. Nothing here may
            // observe the array after that: no ValidateTypedArray (which would raise a TypeError for a
            // buffer detached in the meantime) and no `length` read on an array-like.
            if (_closed)
            {
                nextItem = IteratorResult.CreateKeyValueIteratorPosition(_engine);
                return false;
            }

            ulong len;
            if (_typedArray is not null)
            {
                _typedArray._viewedArrayBuffer.AssertNotDetached();
                var taRecord = IntrinsicTypedArrayPrototype.MakeTypedArrayWithBufferWitnessRecord(_typedArray, ArrayBufferOrder.SeqCst);
                if (taRecord.IsTypedArrayOutOfBounds)
                {
                    Throw.TypeError(_typedArray.Engine.Realm, "TypedArray is out of bounds");
                }
                len = taRecord.TypedArrayLength;
            }
            else
            {
                len = _operations!.GetLongLength();
            }

            if (_position < len)
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
                    // Steps 10.d.v-vi of CreateArrayIterator: a key-kind step reads no element at all, and a
                    // value/entry step is a bare Get -- never a HasProperty first, which on a Proxy or an
                    // array-like host object is an extra observable trap the algorithm does not perform.
                    nextItem = _kind switch
                    {
                        ArrayIteratorType.Key => IteratorResult.CreateValueIteratorPosition(_engine, JsNumber.Create(_position)),
                        ArrayIteratorType.Value => IteratorResult.CreateValueIteratorPosition(_engine, _operations!.Get(_position)),
                        _ => IteratorResult.CreateKeyValueIteratorPosition(_engine, JsNumber.Create(_position), _operations!.Get(_position))
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
        // that method exactly (live length each step, same done/closed transition, the same single
        // Get per step). Typed arrays keep their detach/out-of-bounds guarded path, and Key/Entry
        // kinds keep the wrapping path, both via base.
        internal override bool TryStepValue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out JsValue? value)
        {
            if (_kind == ArrayIteratorType.Value && _typedArray is null)
            {
                if (_closed)
                {
                    value = null;
                    return false;
                }

                var len = _operations!.GetLongLength();
                if (_position < len)
                {
                    var stepped = _operations!.Get(_position);
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
