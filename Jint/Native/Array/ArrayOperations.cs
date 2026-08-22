using System.Collections;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native.Array;

internal abstract class ArrayOperations : IEnumerable<JsValue>
{
    protected internal const ulong MaxArrayLength = 4294967295;
    protected internal const ulong MaxArrayLikeLength = NumberConstructor.MaxSafeInteger;

    public static ArrayOperations For(Realm realm, JsValue value, bool forWrite)
    {
        if (!forWrite)
        {
            if (value.IsString())
            {
                return new JsStringOperations(realm, (JsString) value);
            }

            if (value is StringInstance stringInstance)
            {
                return new JsStringOperations(realm, stringInstance);
            }

            if (value is JsArray { CanUseFastAccess: true } array && array.Length <= (array._dense?.Length ?? -1))
            {
                return new ArrayReadOperations(array);
            }
        }

        return For(TypeConverter.ToObject(realm, value), forWrite);
    }

    /// <summary>
    /// A typed array viewed through its <em>intrinsic</em> length rather than through
    /// <c>LengthOfArrayLike</c>. <c>%TypedArray%.prototype.sort</c> and <c>toSorted</c> are specified on
    /// <c>TypedArrayLength(taRecord)</c> (https://tc39.es/ecma262/#sec-%typedarray%.prototype.sort step 4),
    /// so an own <c>"length"</c> shadowing the prototype accessor must not narrow what they sort -- unlike
    /// the array-like algorithms <see cref="For(ObjectInstance, bool)"/> serves, which all say
    /// <c>LengthOfArrayLike</c>.
    /// </summary>
    public static ArrayOperations ForIntrinsicLength(JsTypedArray instance)
        => new JsTypedArrayOperations(instance, useIntrinsicLength: true);

    public static ArrayOperations For(ObjectInstance instance, bool forWrite)
    {
        if (instance is JsArray { CanUseFastAccess: true } arrayInstance)
        {
            return new JsArrayOperations(arrayInstance);
        }

        if (instance is JsTypedArray typedArrayInstance)
        {
            return new JsTypedArrayOperations(typedArrayInstance);
        }

        // Read-only host collection: every element resolves through one virtual TryGetIndex, with no key object
        // and no descriptor. Reads only — a write-mode dispatch falls through to ObjectOperations so the mutating
        // generics (sort, fill, reverse, …) produce the spec-shaped TypeError an ordinary non-writable property
        // gives, instead of a CLR exception from a lane that cannot write.
        if (!forWrite && instance is ArrayLikeObject arrayLikeObject)
        {
            return new ArrayLikeObjectOperations(arrayLikeObject);
        }

        if (instance is ArrayLikeWrapper arrayWrapper)
        {
            return new ArrayLikeOperations(arrayWrapper);
        }

        if (instance is ObjectWrapper wrapper)
        {
            var descriptor = wrapper._typeDescriptor;
            if (descriptor.IsArrayLike && wrapper.Target is ICollection)
            {
                return new IndexWrappedOperations(wrapper);
            }
        }

        return new ObjectOperations(instance);
    }

    public abstract ObjectInstance Target { get; }

    public abstract ulong GetSmallestIndex(ulong length);

    /// <summary>
    /// <see href="https://tc39.es/ecma262/#sec-lengthofarraylike">LengthOfArrayLike</see>, which is
    /// <c>ToLength(Get(O, "length"))</c> and therefore lands in <c>[0, 2^53-1]</c>
    /// (<see href="https://tc39.es/ecma262/#sec-tolength">ToLength</see>).
    /// <para>
    /// There is deliberately no narrower overload. One used to exist -- a <c>uint</c> sibling every generic
    /// below reached for by name -- and it could not express the specified range at all: it cast the
    /// <c>double</c> across unclamped, which <em>saturates</em> on .NET and is <em>unspecified</em> on .NET
    /// Framework (there it keeps the low 32 bits), so a <c>length</c> of 2^53 was read as 4294967295 on one
    /// target framework and as 0 on the other. That is the same hazard
    /// <see cref="TypeConverter.ToIntegerOrInfinity"/> carries its own note about. Narrow at the call site,
    /// inside a guard that proves the value fits, the way
    /// <see href="https://tc39.es/ecma262/#sec-array.prototype.map">Array.prototype.map</see> does.
    /// </para>
    /// </summary>
    public abstract ulong GetLongLength();

    public abstract void SetLength(ulong length);

    public abstract void EnsureCapacity(ulong capacity);

    public abstract JsValue Get(ulong index);

    public virtual JsValue[] GetAll(
        Types elementTypes = Types.Undefined | Types.Null | Types.Boolean | Types.String | Types.Symbol | Types.Number | Types.BigInt | Types.Object,
        bool skipHoles = false,
        Realm? errorRealm = null)
    {
        uint writeIndex = 0;
        var length = GetLongLength();
        if (length > ClrLimits.MaxArrayLength)
        {
            Throw.RangeError(errorRealm ?? Target.Engine.Realm, "Invalid array-like length");
        }

        var n = (int) length;
        var jsValues = new JsValue[n];
        for (uint i = 0; i < (uint) n; i++)
        {
            // Slow array-like expansion (e.g. Function.prototype.apply / Reflect.apply on a huge
            // {length} object): per-element property Get; check constraints periodically.
            if (i > 0 && i % Engine.ConstraintCheckInterval == 0)
            {
                Target.Engine.Constraints.Check();
            }

            var jsValue = skipHoles && !HasProperty(i) ? JsValue.Undefined : Get(i);
            if ((jsValue.Type & elementTypes) == Types.Empty)
            {
                Throw.TypeErrorNoEngine("invalid type");
            }

            jsValues[writeIndex++] = jsValue;
        }

        return jsValues;
    }

    public abstract bool TryGetValue(ulong index, out JsValue value);

    public abstract bool HasProperty(ulong index);

    public abstract void CreateDataPropertyOrThrow(ulong index, JsValue value);

    public abstract void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true);

    public abstract void DeletePropertyOrThrow(ulong index);

    public ArrayLikeIterator GetEnumerator() => new ArrayLikeIterator(this);

    IEnumerator<JsValue> IEnumerable<JsValue>.GetEnumerator() => new ArrayLikeIterator(this);

    IEnumerator IEnumerable.GetEnumerator() => new ArrayLikeIterator(this);

    internal sealed class ArrayLikeIterator : IEnumerator<JsValue>
    {
        private readonly ArrayOperations _obj;
        private ulong _current;
        private bool _initialized;
        private readonly ulong _length;

        public ArrayLikeIterator(ArrayOperations obj)
        {
            _obj = obj;
            _length = obj.GetLongLength();

            Reset();
        }

        public JsValue Current
        {
            get
            {
                return !_initialized
                    ? JsValue.Undefined
                    : _obj.TryGetValue(_current, out var temp)
                        ? temp
                        : JsValue.Undefined;
            }
        }

        object? IEnumerator.Current => Current;

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            if (!_initialized)
            {
                _initialized = true;
            }
            else
            {
                _current++;
            }

            return _current < _length;
        }

        public void Reset()
        {
            _initialized = false;
            _current = 0;
        }
    }

    private sealed class ObjectOperations : ArrayOperations<ObjectInstance>
    {
        public ObjectOperations(ObjectInstance target) : base(target)
        {
        }

        private double GetIntegerLength()
        {
            var descValue = _target.Get(CommonProperties.Length);
            if (descValue is not null)
            {
                return TypeConverter.ToInteger(descValue);
            }

            return 0;
        }

        public override ulong GetSmallestIndex(ulong length)
        {
            return _target.GetSmallestIndex(length);
        }

        /// <summary>
        /// The only <see cref="ArrayOperations"/> whose length is not bounded by the array index range: an
        /// arbitrary array-like carries whatever <c>ToLength</c> made of its own <c>"length"</c>, so the clamp
        /// at <see cref="MaxArrayLikeLength"/> is what keeps the conversion in range on every target framework.
        /// </summary>
        public override ulong GetLongLength()
        {
            var integerLength = GetIntegerLength();
            if (integerLength <= 0)
            {
                return 0;
            }

            return (ulong) System.Math.Min(integerLength, MaxArrayLikeLength);
        }

        public override void SetLength(ulong length)
            => _target.Set(CommonProperties.Length, length, true);

        public override void EnsureCapacity(ulong capacity)
        {
        }

        public override JsValue Get(ulong index)
            => _target.Get(JsString.Create(index));

        public override bool TryGetValue(ulong index, out JsValue value)
        {
            var propertyName = JsString.Create(index);
            var kPresent = _target.HasProperty(propertyName);
            value = kPresent ? _target.Get(propertyName) : JsValue.Undefined;
            return kPresent;
        }

        public override void CreateDataPropertyOrThrow(ulong index, JsValue value)
            => _target.CreateDataPropertyOrThrow(JsString.Create(index), value);

        public override void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true)
            => _target.Set(JsString.Create(index), value, throwOnError);

        public override void DeletePropertyOrThrow(ulong index)
            => _target.DeletePropertyOrThrow(JsString.Create(index));

        public override bool HasProperty(ulong index) => Target.HasProperty(index);
    }

    private sealed class JsArrayOperations : ArrayOperations<JsArray>
    {
        public JsArrayOperations(JsArray target) : base(target)
        {
        }

        public override ulong GetSmallestIndex(ulong length)
            => _target.GetSmallestIndex();

        public override ulong GetLongLength()
            => _target.GetLongLength();

        public override void SetLength(ulong length)
            => _target.SetLength(length);

        public override void EnsureCapacity(ulong capacity)
            => _target.EnsureCapacity((uint) capacity);

        // array max size is uint
        public override bool TryGetValue(ulong index, out JsValue value)
            => _target.TryGetValue((uint) index, out value);

        public override JsValue Get(ulong index) => _target.Get((uint) index);

        public override JsValue[] GetAll(
            Types elementTypes = Types.Empty | Types.Undefined | Types.Null | Types.Boolean | Types.String | Types.Number | Types.Symbol | Types.BigInt | Types.Object,
            bool skipHoles = false,
            Realm? errorRealm = null)
        {
            var n = _target.GetLength();
            if (n > ClrLimits.MaxArrayLength)
            {
                Throw.RangeError(errorRealm ?? _target.Engine.Realm, "Invalid array-like length");
            }

            if (_target._dense == null || _target._dense.Length < n)
            {
                return base.GetAll(elementTypes, skipHoles, errorRealm);
            }

            // optimized
            uint writeIndex = 0;
            var jsValues = new JsValue[n];
            for (uint i = 0; i < (uint) jsValues.Length; i++)
            {
                var value = _target._dense[i];
                if (value is null)
                {
                    value = _target.Prototype?.Get(i) ?? JsValue.Undefined;
                }

                if ((value.Type & elementTypes) == Types.Empty)
                {
                    Throw.TypeErrorNoEngine("invalid type");
                }

                jsValues[writeIndex++] = (JsValue?) value ?? JsValue.Undefined;
            }

            return jsValues;
        }

        public override void DeletePropertyOrThrow(ulong index)
            => _target.DeletePropertyOrThrow((uint) index);

        public override void CreateDataPropertyOrThrow(ulong index, JsValue value)
        {
            if (!_target.Extensible && !_target.HasOwnIndexProperty(index))
            {
                _target.CreateDataPropertyOrThrow(JsString.Create(index), value);
                return;
            }

            _target.SetIndexValue((uint) index, value, updateLength: false);
        }

        public override void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true)
        {
            // This receiver was chosen because CanUseFastAccess held, which means every element it owns
            // carries the default (writable) descriptor — defining a non-default one raises
            // NonDefaultDataDescriptorUsage and routes the array to ObjectOperations instead. So the only
            // way an element write here can fail [[Set]] is by *creating* an index the array does not
            // already own on a non-extensible receiver. Hand exactly those to the ordinary validating
            // path, which reports the failure — throwing when the caller asked for it — per
            // https://tc39.es/ecma262/#sec-ordinarysetwithowndescriptor step 3.b. Extensible arrays (the
            // overwhelmingly common case) skip straight to the raw store on a single bool test.
            if (!_target.Extensible && !_target.HasOwnIndexProperty(index))
            {
                _target.Set(JsString.Create(index), value, throwOnError);
                return;
            }

            _target.SetIndexValue((uint) index, value, updateLength);
        }

        public override bool HasProperty(ulong index) => _target.HasProperty(index);
    }

    private sealed class JsTypedArrayOperations : ArrayOperations
    {
        private readonly JsTypedArray _target;
        private readonly bool _useIntrinsicLength;

        public JsTypedArrayOperations(JsTypedArray target, bool useIntrinsicLength = false)
        {
            _target = target;
            _useIntrinsicLength = useIntrinsicLength;
        }

        public override ObjectInstance Target => _target;

        public override ulong GetSmallestIndex(ulong length) => 0;

        // This lane serves the *array-like* algorithms -- the Array.prototype generics a typed array can be
        // the receiver of, Array.from, apply-spreading -- and every one of them says
        // "Let len be ? LengthOfArrayLike(O)", which is ToLength(? Get(O, "length"))
        // (https://tc39.es/ecma262/#sec-lengthofarraylike). Usually that resolves to the accessor on
        // %TypedArray%.prototype and agrees with the intrinsic length, but not when the object carries an
        // own "length" of its own: "length" is not a canonical numeric index string, so
        // Object.defineProperty(ta, "length", ...) installs an ordinary own property that shadows the
        // accessor, and Array.prototype.sort.call(ta) must then sort only that many elements.
        // %TypedArray%.prototype's own methods do not come through here -- they read the intrinsic length
        // directly -- so nothing that must ignore an own "length" is affected. The clamp is ToLength's own
        // 2^53-1, not the array index range: a shadowing own "length" is an ordinary property and nothing
        // narrows what LengthOfArrayLike makes of it.
        public override ulong GetLongLength()
        {
            if (_useIntrinsicLength)
            {
                return _target.GetLength();
            }

            var descValue = _target.Get(CommonProperties.Length);
            if (descValue is not null)
            {
                var length = TypeConverter.ToInteger(descValue);
                if (length <= 0)
                {
                    return 0;
                }

                return (ulong) System.Math.Min(length, MaxArrayLikeLength);
            }

            return 0;
        }

        // Every caller of this is a spec step spelled "Perform ? Set(O, "length", len, true)" -- Array.from,
        // Array.fromAsync and the Array.prototype generics that a typed array can be the receiver of. A typed
        // array's "length" is a getter-only accessor on %TypedArray%.prototype, so that Set fails and, being a
        // throwing one, raises a TypeError. Doing nothing here silently swallowed all of them.
        public override void SetLength(ulong length)
            => _target.Set(CommonProperties.Length, length, true);

        public override void EnsureCapacity(ulong capacity)
        {
        }

        public override JsValue Get(ulong index) => _target[(int) index];

        public override bool TryGetValue(ulong index, out JsValue value)
        {
            if (_target.IsValidIntegerIndex(index))
            {
                value = _target[(int) index];
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }

        public override void CreateDataPropertyOrThrow(ulong index, JsValue value)
            => _target.CreateDataPropertyOrThrow(index, value);

        public override void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true)
            => _target[(int) index] = value;

        public override void DeletePropertyOrThrow(ulong index)
            => _target.DeletePropertyOrThrow(index);

        public override bool HasProperty(ulong index) => _target.HasProperty(index);
    }

    private sealed class JsStringOperations : ArrayOperations
    {
        private readonly Realm _realm;
        private readonly JsString _target;
        private ObjectInstance? _wrappedTarget;

        public JsStringOperations(Realm realm, JsString target)
        {
            _realm = realm;
            _target = target;
        }

        public JsStringOperations(Realm realm, StringInstance stringInstance) : this(realm, stringInstance.StringData)
        {
            _wrappedTarget = stringInstance;
        }

        public override ObjectInstance Target => _wrappedTarget ??= _realm.Intrinsics.String.Construct(_target);

        public override ulong GetSmallestIndex(ulong length) => 0;

        public override ulong GetLongLength() => (ulong) _target.Length;

        public override void SetLength(ulong length) => throw new NotSupportedException();

        public override void EnsureCapacity(ulong capacity)
        {
        }

        public override JsValue Get(ulong index) => index < (ulong) _target.Length ? _target[(int) index] : JsValue.Undefined;

        public override bool TryGetValue(ulong index, out JsValue value)
        {
            if (index < (ulong) _target.Length)
            {
                value = _target[(int) index];
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }

        public override bool HasProperty(ulong index) => index < (ulong) _target.Length;

        public override void CreateDataPropertyOrThrow(ulong index, JsValue value) => throw new NotSupportedException();

        public override void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true) => throw new NotSupportedException();

        public override void DeletePropertyOrThrow(ulong index) => throw new NotSupportedException();
    }

    /// <summary>
    /// Read-only lane over a dense <see cref="JsArray"/>: length and backing store are taken once, so an
    /// element read is an array index rather than a property lookup.
    /// <para>
    /// The snapshot is authoritative only for the elements it <em>contains</em>. A <see langword="null"/> slot
    /// is a hole — the absence of an own element, not the value <c>undefined</c> — and resolving it is
    /// <c>Get(O, ToString(k))</c> on the array itself, which is what <c>JsArray.Get(uint)</c> performs. That
    /// has to happen when the element's turn comes, because a generic that runs user code per element (an
    /// element's <c>toString</c> under <c>Array.prototype.join</c>, most visibly) can install the index on
    /// the prototype chain between the snapshot and the read.
    /// </para>
    /// </summary>
    private sealed class ArrayReadOperations : ArrayOperations
    {
        private readonly JsArray _target;
        private readonly JsValue?[] _data;
        private readonly uint _length;

        public ArrayReadOperations(JsArray target)
        {
            _target = target;
            _data = target._dense ?? [];
            _length = target.Length;
        }

        public override ObjectInstance Target => _target;

        public override ulong GetSmallestIndex(ulong length) => 0;

        public override ulong GetLongLength() => _length;

        public override void SetLength(ulong length) => throw new NotSupportedException();

        public override void EnsureCapacity(ulong capacity)
        {
        }

        public override JsValue Get(ulong index)
        {
            if (index < (ulong) _data.Length)
            {
                var value = _data[(int) index];
                if (value is not null)
                {
                    return value;
                }
            }

            // Hole, or past the snapshot: while CanUseFastAccess still holds nothing can shadow the
            // hole, so the answer is undefined and is given inline - JsArray.Get(uint) would conclude
            // the same after re-probing the dense store, and its non-inlined call was measured at +55%
            // on a hole-heavy join. The flag is re-read on every hole because a side effect during this
            // very join is exactly what can clear it; once it is gone, ask the array for the spec's
            // real chain-walking read.
            if (_target.CanUseFastAccess)
            {
                return JsValue.Undefined;
            }

            return index <= uint.MaxValue ? _target.Get((uint) index) : JsValue.Undefined;
        }

        public override bool TryGetValue(ulong index, out JsValue value)
        {
            if (index < _length)
            {
                value = _data[(int) index]!;
                return value is not null;
            }

            value = JsValue.Undefined;
            return false;
        }

        public override bool HasProperty(ulong index) => index < _length && _data[index] is not null;

        public override void CreateDataPropertyOrThrow(ulong index, JsValue value) => throw new NotSupportedException();

        public override void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true) => throw new NotSupportedException();

        public override void DeletePropertyOrThrow(ulong index) => throw new NotSupportedException();
    }

    private sealed class IndexWrappedOperations : ArrayOperations
    {
        private readonly ObjectWrapper _target;
        private readonly ICollection _collection;
        private readonly IList _list;

        public IndexWrappedOperations(ObjectWrapper wrapper)
        {
            _target = wrapper;
            _collection = (ICollection) wrapper.Target;
            _list = (IList) wrapper.Target;
        }

        public override ObjectInstance Target => _target;

        public override ulong GetSmallestIndex(ulong length) => 0;

        public override ulong GetLongLength() => (ulong) _collection.Count;

        public override void SetLength(ulong length)
        {
            if (!_target.Engine.Options.Interop.AllowWrite || !_target.Extensible)
            {
                _target.Set(CommonProperties.Length, length, true);
                return;
            }

            if (_list == null)
            {
                throw new NotSupportedException();
            }

            while (_list.Count > (int) length)
            {
                // shrink list to fit
                _list.RemoveAt(_list.Count - 1);
            }

            while (_list.Count < (int) length)
            {
                // expand list to fit
                _list.Add(null);
            }
        }

        public override void EnsureCapacity(ulong capacity)
        {
        }

        public override JsValue Get(ulong index) => index < (ulong) _collection.Count ? ReadValue((int) index) : JsValue.Undefined;

        public override bool TryGetValue(ulong index, out JsValue value)
        {
            if (index < (ulong) _collection.Count)
            {
                value = ReadValue((int) index);
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }

        private JsValue ReadValue(int index)
        {
            if (_list is not null)
            {
                return (uint) index < _list.Count ? JsValue.FromObject(_target.Engine, _list[index]) : JsValue.Undefined;
            }

            // via reflection is slow, but better than nothing
            return JsValue.FromObject(_target.Engine, _target._typeDescriptor.IntegerIndexerProperty!.GetValue(Target, [index]));
        }

        public override bool HasProperty(ulong index) => index < (ulong) _collection.Count;

        public override void CreateDataPropertyOrThrow(ulong index, JsValue value)
            => _target.CreateDataPropertyOrThrow(index, value);

        public override void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true)
        {
            if (updateLength && _list != null && index >= (ulong) _list.Count)
            {
                SetLength(index + 1);
            }

            _target.Set(JsString.Create(index), value, throwOnError);
        }

        public override void DeletePropertyOrThrow(ulong index)
            => _target.DeletePropertyOrThrow(index);
    }

    /// <summary>
    /// Read side of a host <see cref="ArrayLikeObject"/>: length and elements come straight off the two abstract
    /// members, so an <c>Array.prototype</c> generic, the array iterator (for-of, spread, <c>Array.from</c>,
    /// destructuring) or <c>apply</c>-spreading costs one virtual call per element and allocates no key.
    /// <para>
    /// A <c>false</c> from <c>TryGetIndex</c> is an authoritative own miss, not "no such element anywhere", so the
    /// miss path re-asks the object the ordinary way and the prototype chain still resolves an index the host does
    /// not own. That costs nothing on the hit path, which is the one that runs per element.
    /// </para>
    /// </summary>
    private sealed class ArrayLikeObjectOperations : ArrayOperations<ArrayLikeObject>
    {
        public ArrayLikeObjectOperations(ArrayLikeObject target) : base(target)
        {
        }

        public override ulong GetSmallestIndex(ulong length) => 0;

        public override ulong GetLongLength() => _target.Length;

        public override void SetLength(ulong length) => _target.Set(CommonProperties.Length, length, true);

        public override void EnsureCapacity(ulong capacity)
        {
        }

        public override JsValue Get(ulong index)
            => index < uint.MaxValue && _target.ReadIndex((uint) index, out var value)
                ? value
                : _target.Get(JsString.Create(index));

        public override bool TryGetValue(ulong index, out JsValue value)
        {
            if (index < uint.MaxValue && _target.ReadIndex((uint) index, out value))
            {
                return true;
            }

            var property = JsString.Create(index);
            if (_target.HasProperty(property))
            {
                value = _target.Get(property);
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }

        // Existence only — this is the hole test the generics run per element, so it goes through the host's
        // containment hook rather than producing an element to discard.
        public override bool HasProperty(ulong index)
            => (index < uint.MaxValue && _target.ProbeIndex((uint) index)) || _target.HasProperty(JsString.Create(index));

        public override void CreateDataPropertyOrThrow(ulong index, JsValue value)
            => _target.CreateDataPropertyOrThrow(JsString.Create(index), value);

        public override void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true)
            => _target.Set(JsString.Create(index), value, throwOnError);

        public override void DeletePropertyOrThrow(ulong index)
            => _target.DeletePropertyOrThrow(JsString.Create(index));
    }

    private sealed class ArrayLikeOperations : ArrayOperations
    {
        private readonly ArrayLikeWrapper _target;

        public ArrayLikeOperations(ArrayLikeWrapper wrapper)
        {
            _target = wrapper;
        }

        public override ObjectInstance Target => _target;

        public override ulong GetSmallestIndex(ulong length) => 0;

        public override ulong GetLongLength() => (ulong) _target.Length;

        public override void SetLength(ulong length) => _target.Set(CommonProperties.Length, length, true);

        public override void EnsureCapacity(ulong capacity)
        {
            if (capacity > (ulong) _target.Length)
            {
                SetLength(capacity);
            }
        }

        public override JsValue Get(ulong index) => index < (ulong) _target.Length ? ReadValue((int) index) : JsValue.Undefined;

        public override bool TryGetValue(ulong index, out JsValue value)
        {
            if (index < (ulong) _target.Length)
            {
                value = ReadValue((int) index);
                return true;
            }

            value = JsValue.Undefined;
            return false;
        }

        private JsValue ReadValue(int index)
        {
            // GetJsValueAt converts common primitive element types without boxing
            return (uint) index < _target.Length ? _target.GetJsValueAt(index) : JsValue.Undefined;
        }

        public override bool HasProperty(ulong index) => index < (ulong) _target.Length;

        public override void CreateDataPropertyOrThrow(ulong index, JsValue value)
            => _target.CreateDataPropertyOrThrow(index, value);

        public override void Set(ulong index, JsValue value, bool updateLength = false, bool throwOnError = true)
        {
            if (_target.Engine.Options.Interop.AllowWrite && _target.Extensible)
            {
                _target.SetAt((int) index, value);
            }
            else
            {
                _target.Set(JsNumber.Create(index), value, throwOnError);
            }
        }

        public override void DeletePropertyOrThrow(ulong index)
            => _target.DeletePropertyOrThrow(index);
    }
}

/// <summary>
///     Adapter to use optimized array operations when possible.
///     Gaps the difference between ArgumentsInstance and ArrayInstance.
/// </summary>
internal abstract class ArrayOperations<T> : ArrayOperations where T : ObjectInstance
{
    protected readonly T _target;

    protected ArrayOperations(T target)
    {
        _target = target;
    }

    public override ObjectInstance Target => _target;
}
