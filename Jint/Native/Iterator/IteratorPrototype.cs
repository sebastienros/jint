using System.Runtime.CompilerServices;
using System.Text;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Pooling;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using FunctionInstance = Jint.Native.Function.Function;

namespace Jint.Native.Iterator;

/// <summary>
/// https://tc39.es/ecma262/#sec-%iteratorprototype%-object
/// </summary>
[JsObject(UseShape = true)]
internal partial class IteratorPrototype : Prototype
{
    // A built-in iterator prototype's own `next`, captured by its CreateProperties_Generated (CaptureField on
    // its [JsFunction(Name = "next")]) before any script can replace it. Null on a prototype that captures
    // nothing, for which both methods below answer an ObjectIterator and the iteration goes through the `next`
    // it reads.
    private protected FunctionInstance? _originalNextFunction;

    // Stands in for an own `next` that is not a data property; its value is never a function.
    private static readonly PropertyDescriptor NotADataProperty = new(Undefined, PropertyFlag.None);

    // The own `next` data descriptor as it stood at _nextDescriptorVersion (NotADataProperty if it was not one), or
    // null while it has not been read - PromisePrototype.HasIntrinsicConstructor's pattern. Replacing, deleting or
    // redefining `next` as an accessor goes through SetOwnProperty, SetProperty or RemoveOwnProperty and moves
    // _propertiesVersion; an assignment, or a redefinition that keeps it a data property, changes this descriptor's
    // value in place, which is re-read every time.
    private PropertyDescriptor? _nextDescriptor;
    private uint _nextDescriptorVersion;

    // %Iterator.prototype%'s own @@iterator, captured by its CreateSymbols_Generated (CaptureField on the
    // [JsSymbolFunction("Iterator")] below). Null on every other prototype, which is what makes an @@iterator found on
    // one of those answer the specified path.
    private FunctionInstance? _originalIteratorFunction;

    // The @@iterator lookup InheritsOriginalIteratorMethod caches: the data descriptor found (NotADataProperty if
    // none), the object it was found on, both objects' own-property versions and this prototype's own prototype as
    // they stood then. Null while it has not been looked up.
    private PropertyDescriptor? _iteratorMethodDescriptor;
    private IteratorPrototype? _iteratorMethodHolder;
    private ObjectInstance? _iteratorMethodPrototype;
    private uint _iteratorMethodVersion;
    private uint _iteratorMethodHolderVersion;

    /// <summary>
    /// GetIteratorFromMethod (https://tc39.es/ecma262/#sec-getiteratorfrommethod) step 3, GetIteratorDirect steps 1-2,
    /// for <paramref name="iterator"/>, whose prototype this is: the one read of <c>next</c> off it, and the record.
    /// One read covers both ways it can stop finding this prototype's built-in <c>next</c> — an own <c>next</c> on the
    /// instance shadowing the prototype's, and the prototype's own <c>next</c> replaced — and answers
    /// <paramref name="iterator"/> itself while it still does. Otherwise the record is an
    /// <see cref="IteratorInstance.ObjectIterator"/> holding what was read, which never reads it again
    /// (sebastienros/jint#4408). The array iterators ask it, and <see cref="ToIteratorRecord"/> falls back to it.
    /// </summary>
    private protected IteratorInstance ToIteratorRecordReadingNext(IteratorInstance iterator)
    {
        var next = iterator.Get(CommonProperties.Next);
        return ReferenceEquals(next, _originalNextFunction)
            ? iterator
            : IteratorInstance.WrapReplacedNext(iterator, next);
    }

    /// <summary>
    /// The record <see cref="ToIteratorRecordReadingNext"/> builds for <paramref name="iterator"/>, whose prototype
    /// this is, without its property read when the answer can be had cheaper: an instance with no own property
    /// cannot shadow <c>next</c>, so it steps natively exactly while this prototype's own <c>next</c> is still a data
    /// property holding the built-in function, which a cached descriptor and this prototype's own-property version
    /// answer. When it is not, nothing has been read and the <see cref="IteratorInstance.ObjectIterator"/> makes the
    /// one read. An instance that has an own property takes the read here.
    /// <see cref="IteratorInstance.ToIteratorRecord"/> asks it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IteratorInstance ToIteratorRecord(IteratorInstance iterator)
    {
        if (iterator._properties is not null)
        {
            return ToIteratorRecordReadingNext(iterator);
        }

        return HasOriginalNext()
            ? iterator
            : IteratorInstance.WrapReadingNext(iterator);
    }

    /// <summary>
    /// Whether <c>next</c>, read off an instance of this prototype that has no own property, finds this prototype's
    /// built-in <c>next</c>: its own <c>next</c> is still a data property holding that function. A cached descriptor and
    /// this prototype's own-property version answer it without a property read. <see cref="ToIteratorRecord"/> asks it,
    /// as does the string primitive's lane, whose iterator is not yet an object anyone could have written to
    /// (<c>StringIteratorPrototype.ToIteratorRecord</c>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private protected bool HasOriginalNext()
    {
        var descriptor = _nextDescriptor;
        if (descriptor is null || _propertiesVersion != _nextDescriptorVersion)
        {
            descriptor = ReadNextDescriptor();
        }

        return ReferenceEquals(descriptor.Value, _originalNextFunction);
    }

    // The `return` lookup IteratorClose makes on an instance of this prototype that has no own property, as it stood
    // when it was last made: this prototype's own-property version, the two links above it and their versions, and
    // the answer - whether none of the three had an own `return`. _returnLookupGrandparent is null while nothing is
    // cached, which is also every chain that is not exactly three links long, or that has a link whose version cannot
    // witness a `return` joining it.
    private ObjectInstance? _returnLookupParent;
    private ObjectInstance? _returnLookupGrandparent;
    private uint _returnLookupVersion;
    private uint _returnLookupParentVersion;
    private uint _returnLookupGrandparentVersion;
    private bool _inheritsNoReturnMethod;

    /// <summary>
    /// IteratorClose step 3 (https://tc39.es/ecma262/#sec-iteratorclose), GetMethod(iterator, "return"), for an
    /// instance of this prototype that has no own property: whether the read would answer <c>undefined</c> because
    /// nothing on the chain above has a <c>return</c>. No built-in iterator prototype has one, so this is the answer
    /// for every iterator nobody touched, and it is cached, so that closing one reads no property.
    /// <para>
    /// The cached answer holds while this prototype, its prototype and that one's prototype are the objects they were,
    /// with the own-property versions they had, and the third has no prototype - the shape of every built-in
    /// iterator's chain, which ends at <c>Object.prototype</c>. Adding, deleting or redefining <c>return</c> on any of
    /// them as data or as an accessor moves its version; <c>setPrototypeOf</c> on either of the first two changes a
    /// link. An answer of <see langword="false"/> is cached too, and only sends the caller to the specified read. The
    /// array destructuring lanes ask it of <c>%ArrayIteratorPrototype%</c> before they read an array by index in place
    /// of its iterator (sebastienros/jint#4406).
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool InstancesInheritNoReturnMethod()
    {
        // Until a script has put a `return` on a built-in iterator prototype or on Object.prototype, or moved one of
        // these prototypes' [[Prototype]], no built-in chain has one: Engine._builtinIteratorChainMayHaveReturn. That
        // test is all a caller inlines; the version-checked answer is out of line.
        return !_engine._builtinIteratorChainMayHaveReturn || InstancesInheritNoReturnMethodOnceTouched();
    }

    /// <summary>
    /// <see cref="InstancesInheritNoReturnMethod"/> once <see cref="Engine._builtinIteratorChainMayHaveReturn"/> is set:
    /// the cached answer while its version keys hold, otherwise a fresh lookup.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool InstancesInheritNoReturnMethodOnceTouched()
    {
        var grandparent = _returnLookupGrandparent;
        var parent = _prototype;
        if (grandparent is not null
            && _propertiesVersion == _returnLookupVersion
            && ReferenceEquals(parent, _returnLookupParent)
            && parent!._propertiesVersion == _returnLookupParentVersion
            && ReferenceEquals(parent._prototype, grandparent)
            && grandparent._propertiesVersion == _returnLookupGrandparentVersion
            && grandparent._prototype is null)
        {
            return _inheritsNoReturnMethod;
        }

        return LookUpReturnMethod();
    }

    /// <summary>
    /// Whether <see cref="InstancesInheritNoReturnMethod"/> has an answer cached; for tests that pin the cached lane.
    /// </summary>
    internal bool CachesReturnLookup => _returnLookupGrandparent is not null;

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool LookUpReturnMethod()
    {
        _returnLookupGrandparent = null;

        // GetOwnProperty first on each link: it initializes a lazily built prototype on first use, and the version
        // recorded is the one after that. A link whose own reads are not ordinary - a Proxy, a host object - would see
        // the probe, so the walk stops in front of it, and the caller makes the read.
        var parent = _prototype;
        if (!TryProbeForReturn(this, out var returnFound)
            || parent is null
            || !TryProbeForReturn(parent, out var returnOnParent))
        {
            return false;
        }

        var grandparent = parent._prototype;
        if (grandparent is null
            || !TryProbeForReturn(grandparent, out var returnOnGrandparent)
            || grandparent._prototype is not null)
        {
            return false;
        }

        _inheritsNoReturnMethod = !returnFound && !returnOnParent && !returnOnGrandparent;
        _returnLookupVersion = _propertiesVersion;
        _returnLookupParent = parent;
        _returnLookupParentVersion = parent._propertiesVersion;
        _returnLookupGrandparentVersion = grandparent._propertiesVersion;
        _returnLookupGrandparent = grandparent;
        return _inheritsNoReturnMethod;
    }

    /// <summary>
    /// Whether <paramref name="link"/> has an own <c>return</c>, answered only when nothing observes the question and
    /// the link's own-property version moves when a <c>return</c> joins or leaves it, so that a cached answer can be
    /// checked against it (<see cref="ObjectInstance.VersionWitnessesOwnProperty"/>). Both are read off the link's type
    /// flags before anything is asked of it: any other link - a Proxy, a host object, which may count or fail the
    /// question - is refused unprobed, and the caller's GetMethod is the only read it sees.
    /// </summary>
    private static bool TryProbeForReturn(ObjectInstance link, out bool found)
    {
        if ((link._type & (InternalTypes.ExoticGet | InternalTypes.OwnValueHook)) != InternalTypes.Empty
            || !VersionWitnessesOwnProperty(link, CommonProperties.Return))
        {
            found = false;
            return false;
        }

        found = !ReferenceEquals(link.GetOwnProperty(CommonProperties.Return), PropertyDescriptor.Undefined);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private PropertyDescriptor ReadNextDescriptor()
    {
        // GetOwnProperty first: it initializes the prototype (capturing _originalNextFunction) on first use, and the
        // version is the one after that.
        var descriptor = GetOwnProperty(CommonProperties.Next);
        if (!descriptor.IsDataDescriptor())
        {
            descriptor = NotADataProperty;
        }

        _nextDescriptorVersion = _propertiesVersion;
        _nextDescriptor = descriptor;
        return descriptor;
    }

    /// <summary>
    /// GetIterator (https://tc39.es/ecma262/#sec-getiterator) step for an instance of this prototype that has no own
    /// symbol-keyed property: whether the <c>@@iterator</c> it inherits is %Iterator.prototype%'s own built-in method
    /// (https://tc39.es/ecma262/#sec-iterator.prototype-%symbol.iterator%), which answers its receiver. Then reading and
    /// calling it is unobservable, and the instance is the object GetIteratorFromMethod reads <c>next</c> off.
    /// <para>
    /// The lookup goes one link up at most: this prototype's own <c>@@iterator</c>, else its prototype's when that is a
    /// built-in iterator prototype. Anything else answers <see langword="false"/>, and the caller takes the specified
    /// path. The descriptor found is cached with both objects' own-property versions, which a symbol-keyed change moves
    /// here (<see cref="SetOwnProperty"/>), and with this prototype's own prototype, which <c>setPrototypeOf</c> moves.
    /// An assignment or a data redefinition changes the cached descriptor's value in place, which is re-read every time.
    /// <c>IteratorInstance.TryGetIterator</c> asks it (sebastienros/jint#4405).
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool InheritsOriginalIteratorMethod()
    {
        var descriptor = _iteratorMethodDescriptor;
        var holder = _iteratorMethodHolder;
        if (descriptor is null
            || _propertiesVersion != _iteratorMethodVersion
            || !ReferenceEquals(_prototype, _iteratorMethodPrototype)
            || holder!._propertiesVersion != _iteratorMethodHolderVersion)
        {
            descriptor = LookUpIteratorMethod();
            holder = _iteratorMethodHolder;
        }

        return ReferenceEquals(descriptor.Value, holder!._originalIteratorFunction);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private PropertyDescriptor LookUpIteratorMethod()
    {
        // GetOwnProperty first, on each object: it initializes the prototype (capturing %Iterator.prototype%'s
        // _originalIteratorFunction) on first use, and the versions are the ones after that.
        var holder = this;
        var descriptor = GetOwnProperty(GlobalSymbolRegistry.Iterator);
        if (ReferenceEquals(descriptor, PropertyDescriptor.Undefined) && _prototype is IteratorPrototype parent)
        {
            holder = parent;
            descriptor = parent.GetOwnProperty(GlobalSymbolRegistry.Iterator);
        }

        if (!descriptor.IsDataDescriptor())
        {
            descriptor = NotADataProperty;
        }

        _iteratorMethodHolder = holder;
        _iteratorMethodHolderVersion = holder._propertiesVersion;
        _iteratorMethodPrototype = _prototype;
        _iteratorMethodVersion = _propertiesVersion;
        _iteratorMethodDescriptor = descriptor;
        return descriptor;
    }

    /// <summary>
    /// A symbol-keyed own property is stored apart from the string-keyed ones, and adding, replacing or removing one
    /// does not move <see cref="ObjectInstance._propertiesVersion"/>. On a built-in iterator prototype it does, because
    /// the cached <c>@@iterator</c> lookup is validated against it (<see cref="InheritsOriginalIteratorMethod"/>).
    /// Every script route to a new descriptor comes through here or <see cref="RemoveOwnProperty"/>, which is also why a
    /// <c>return</c> put here sets <see cref="Engine._builtinIteratorChainMayHaveReturn"/>, from which point
    /// <see cref="InstancesInheritNoReturnMethod"/> answers from its version-checked lookup.
    /// </summary>
    protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        if (CommonProperties.Return.Equals(property))
        {
            _engine._builtinIteratorChainMayHaveReturn = true;
        }

        base.SetOwnProperty(property, desc);
        if (property.IsSymbol())
        {
            unchecked { _propertiesVersion++; }
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinarysetprototypeof. A built-in iterator prototype whose [[Prototype]] moves no
    /// longer has the chain <see cref="Engine._builtinIteratorChainMayHaveReturn"/> vouches for, so a change sets it.
    /// </summary>
    internal override bool SetPrototypeOf(JsValue value)
    {
        if (!ReferenceEquals(value, _prototype ?? Null))
        {
            _engine._builtinIteratorChainMayHaveReturn = true;
        }

        return base.SetPrototypeOf(value);
    }

    /// <inheritdoc cref="SetOwnProperty"/>
    public override void RemoveOwnProperty(JsValue property)
    {
        base.RemoveOwnProperty(property);
        if (property.IsSymbol())
        {
            unchecked { _propertiesVersion++; }
        }
    }

    internal IteratorPrototype(
        Engine engine,
        Realm realm,
        ObjectInstance objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-iterator.prototype.constructor
    /// </summary>
    [JsAccessor("constructor")]
    private IteratorConstructor ConstructorGet(JsValue thisObject) => _engine.Intrinsics.Iterator;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set-iterator.prototype.constructor
    /// </summary>
    [JsAccessor("constructor", AccessorKind.Set)]
    private JsValue ConstructorSet(JsValue thisObject, JsValue value)
    {
        SetterThatIgnoresPrototypeProperties(thisObject, _engine.Intrinsics.IteratorPrototype, CommonProperties.Constructor, value);
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-iterator.prototype-%symbol.tostringtag%
    /// </summary>
    private static readonly JsString IteratorToStringTag = new("Iterator");

    [JsSymbolAccessor("ToStringTag")]
    private static JsString ToStringTagGet(JsValue thisObject) => IteratorToStringTag;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set-iterator.prototype-%symbol.tostringtag%
    /// </summary>
    [JsSymbolAccessor("ToStringTag", AccessorKind.Set)]
    private JsValue ToStringTagSet(JsValue thisObject, JsValue value)
    {
        SetterThatIgnoresPrototypeProperties(thisObject, _engine.Intrinsics.IteratorPrototype, GlobalSymbolRegistry.ToStringTag, value);
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-SetterThatIgnoresPrototypeProperties
    /// </summary>
    private void SetterThatIgnoresPrototypeProperties(JsValue thisValue, ObjectInstance home, JsValue p, JsValue v)
    {
        if (thisValue is not ObjectInstance objectInstance)
        {
            Throw.TypeError(_realm, "Iterator prototype setter called on non-object");
            return;
        }

        if (SameValue(thisValue, home))
        {
            Throw.TypeError(_realm, "Cannot set property on Iterator prototype directly");
            return;
        }

        // step 4's `desc is undefined` is the only thing the descriptor was ever consulted for
        if (!objectInstance.HasOwnProperty(p))
        {
            objectInstance.CreateDataPropertyOrThrow(p, v);
        }
        else
        {
            objectInstance.Set(p, v, throwOnError: true);
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.map
    /// </summary>
    [JsFunction]
    private JsValue Map(JsValue thisObject, JsValue mapper)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.map called on non-object");
            return Undefined;
        }

        // 3. If IsCallable(mapper) is false, throw a TypeError exception.
        ICallable mapperCallable;
        try
        {
            mapperCallable = GetCallable(mapper);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 4. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return map iterator
        return new MapIterator(_engine, iterated, mapperCallable);
    }

    private static IteratorInstance.ObjectIterator GetIteratorDirect(ObjectInstance objectInstance) => new(objectInstance);

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iteratorclose
    /// Close an iterator by calling its return() method directly (without reading next).
    /// Used when validation fails before GetIteratorDirect is called.
    /// </summary>
    private static void IteratorClose(ObjectInstance obj, CompletionType completionType)
    {
        // Step 4 - "If completion is a throw completion, return ? completion" - is reached before
        // innerResult is inspected, so nothing raised while closing may replace the error that
        // triggered the close. That covers the GetMethod in step 2 as much as the call in step 3.c:
        // a throwing "return" accessor, or a "return" that is present but not callable, must leave
        // the original TypeError/RangeError/coercion error standing.
        ICallable? returnMethod;
        try
        {
            returnMethod = obj.GetMethod(CommonProperties.Return);
        }
        catch when (completionType == CompletionType.Throw)
        {
            return;
        }

        if (returnMethod is null)
        {
            return;
        }

        try
        {
            returnMethod.Call(obj, Arguments.Empty);
        }
        catch when (completionType == CompletionType.Throw)
        {
            // Ignore errors from return when completion is throw
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.filter
    /// </summary>
    [JsFunction]
    private JsValue Filter(JsValue thisObject, JsValue predicate)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.filter called on non-object");
            return Undefined;
        }

        // 3. If IsCallable(predicate) is false, throw a TypeError exception.
        ICallable predicateCallable;
        try
        {
            predicateCallable = GetCallable(predicate);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 4. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return filter iterator
        return new FilterIterator(_engine, iterated, predicateCallable);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.take
    /// </summary>
    [JsFunction]
    private JsValue Take(JsValue thisObject, JsValue limit)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.take called on non-object");
            return Undefined;
        }

        // 3. Let iterated be the Iterator Record { [[Iterator]]: O, [[NextMethod]]: undefined, [[Done]]: false }.
        //    The record is modelled by the receiver alone, because every close below happens before
        //    GetIteratorDirect and so must read "return" without ever touching "next".
        // 4. Let numLimit be Completion(ToNumber(limit)).
        // 5. IfAbruptCloseIterator(numLimit, iterated).
        double numLimit;
        try
        {
            numLimit = TypeConverter.ToNumber(limit);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 6. If numLimit is NaN, close the iterator and throw a RangeError exception.
        if (double.IsNaN(numLimit))
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, "NaN must be positive");
            return Undefined;
        }

        // 7. If numLimit is finite and numLimit > 𝔽(2**53 - 1), close the iterator and throw a
        //    RangeError exception. Infinity stays valid and means an unbounded limit.
        if (double.IsFinite(numLimit) && numLimit > NumberConstructor.MaxSafeInteger)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, $"{TypeConverter.ToString(numLimit)} exceeds the maximum safe integer");
            return Undefined;
        }

        // 8. Let integerLimit be ! ToIntegerOrInfinity(numLimit).
        var integerLimit = TypeConverter.ToIntegerOrInfinity(numLimit);

        // 9. If integerLimit < 0, close the iterator and throw a RangeError exception.
        if (integerLimit < 0)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, $"{TypeConverter.ToString(integerLimit)} must be positive");
            return Undefined;
        }

        // 10. Set iterated to ? GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return take iterator
        var lim = double.IsPositiveInfinity(integerLimit) ? long.MaxValue : (long) integerLimit;
        return new TakeIterator(_engine, iterated, lim);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.drop
    /// </summary>
    [JsFunction]
    private JsValue Drop(JsValue thisObject, JsValue limit)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.drop called on non-object");
            return Undefined;
        }

        // 3. Let iterated be the Iterator Record { [[Iterator]]: O, [[NextMethod]]: undefined, [[Done]]: false }.
        //    The record is modelled by the receiver alone, because every close below happens before
        //    GetIteratorDirect and so must read "return" without ever touching "next".
        // 4. Let numLimit be Completion(ToNumber(limit)).
        // 5. IfAbruptCloseIterator(numLimit, iterated).
        double numLimit;
        try
        {
            numLimit = TypeConverter.ToNumber(limit);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 6. If numLimit is NaN, close the iterator and throw a RangeError exception.
        if (double.IsNaN(numLimit))
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, "NaN must be positive");
            return Undefined;
        }

        // 7. If numLimit is finite and numLimit > 𝔽(2**53 - 1), close the iterator and throw a
        //    RangeError exception. Infinity stays valid and means an unbounded limit.
        if (double.IsFinite(numLimit) && numLimit > NumberConstructor.MaxSafeInteger)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, $"{TypeConverter.ToString(numLimit)} exceeds the maximum safe integer");
            return Undefined;
        }

        // 8. Let integerLimit be ! ToIntegerOrInfinity(numLimit).
        var integerLimit = TypeConverter.ToIntegerOrInfinity(numLimit);

        // 9. If integerLimit < 0, close the iterator and throw a RangeError exception.
        if (integerLimit < 0)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, $"{TypeConverter.ToString(integerLimit)} must be positive");
            return Undefined;
        }

        // 10. Set iterated to ? GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return drop iterator
        var lim = double.IsPositiveInfinity(integerLimit) ? long.MaxValue : (long) integerLimit;
        return new DropIterator(_engine, iterated, lim);
    }

    /// <summary>
    /// https://tc39.es/proposal-iterator-chunking/#sec-iterator.prototype.chunks
    /// </summary>
    [JsFunction]
    private JsValue Chunks(JsValue thisObject, JsValue chunkSize)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.chunks called on non-object");
            return Undefined;
        }

        // 3-6. Validate chunkSize against the pre-GetIteratorDirect Iterator Record.
        var size = ValidateChunkOrWindowSize(o, chunkSize, "chunkSize");

        // 7. Set iterated to ? GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        return new ChunksIterator(_engine, iterated, size);
    }

    /// <summary>
    /// https://tc39.es/proposal-iterator-chunking/#sec-iterator.prototype.windows
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Windows(JsValue thisObject, JsValue windowSize, JsValue undersized)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.windows called on non-object");
            return Undefined;
        }

        // 3-6. Validate windowSize first - an invalid size is a RangeError even when undersized is
        //      also invalid, so the two checks may not be reordered.
        var size = ValidateChunkOrWindowSize(o, windowSize, "windowSize");

        // 7. If undersized is undefined, set undersized to "only-full".
        // 8. If undersized is neither "only-full" nor "allow-partial", close the iterator and throw
        //    a TypeError exception. The value is compared as-is: there is no ToString coercion, so
        //    a String object or anything else non-string is a TypeError rather than a match.
        var allowPartial = false;
        if (!undersized.IsUndefined())
        {
            if (undersized is not JsString mode)
            {
                IteratorClose(o, CompletionType.Throw);
                Throw.TypeError(_realm, "undersized must be either \"only-full\" or \"allow-partial\"");
                return Undefined;
            }

            switch (mode.ToString())
            {
                case "only-full":
                    break;
                case "allow-partial":
                    allowPartial = true;
                    break;
                default:
                    IteratorClose(o, CompletionType.Throw);
                    Throw.TypeError(_realm, "undersized must be either \"only-full\" or \"allow-partial\"");
                    return Undefined;
            }
        }

        // 9. Set iterated to ? GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        return new WindowsIterator(_engine, iterated, size, allowPartial);
    }

    /// <summary>
    /// Shared steps 4-6 of Iterator.prototype.chunks and Iterator.prototype.windows.
    /// https://tc39.es/proposal-iterator-chunking/#sec-iterator.prototype.chunks
    /// </summary>
    /// <remarks>
    /// The size is taken verbatim, with no ToNumber coercion: anything that is not already an
    /// integral Number is a TypeError, which is why NaN and ±Infinity land there rather than in the
    /// range check below. Every failure closes the receiver through "return" without reading "next",
    /// because the spec's Iterator Record still carries an undefined [[NextMethod]] at this point.
    /// </remarks>
    private uint ValidateChunkOrWindowSize(ObjectInstance o, JsValue size, string parameterName)
    {
        // 4. If size is not a Number, close the iterator and throw a TypeError exception.
        // 5. If size is not an integral Number, close the iterator and throw a TypeError exception.
        if (size is not JsNumber number || !TypeConverter.IsIntegralNumber(number._value))
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.TypeError(_realm, $"{parameterName} must be an integral Number");
            return 0;
        }

        // 6. If size is not in the inclusive interval from 1𝔽 to 𝔽(2**32 - 1), close the iterator
        //    and throw a RangeError exception. -0 is integral but below 1, so it lands here.
        var value = number._value;
        if (value < 1 || value > uint.MaxValue)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, $"{parameterName} must be in the inclusive interval from 1 to 2**32 - 1");
            return 0;
        }

        return (uint) value;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.flatmap
    /// </summary>
    [JsFunction]
    private JsValue FlatMap(JsValue thisObject, JsValue mapper)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.flatMap called on non-object");
            return Undefined;
        }

        // 3. If IsCallable(mapper) is false, throw a TypeError exception.
        ICallable mapperCallable;
        try
        {
            mapperCallable = GetCallable(mapper);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 4. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // Create and return flatMap iterator
        return new FlatMapIterator(_engine, iterated, mapperCallable);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.reduce
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Reduce(JsValue thisObject, JsCallArguments arguments)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.reduce called on non-object");
            return Undefined;
        }

        // 3. If IsCallable(reducer) is false, throw a TypeError exception.
        ICallable reducer;
        try
        {
            reducer = GetCallable(arguments.At(0));
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // 4. Let iterated be GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        JsValue accumulator;
        var counter = 0;

        // 5. If initialValue is not present, then
        if (arguments.Length < 2)
        {
            // a. Let next be ? IteratorStep(iterated).
            // b. If next is done, throw a TypeError exception.
            if (!iterated.TryIteratorStep(out var firstResult))
            {
                Throw.TypeError(_realm, "Reduce of empty iterator with no initial value");
                return Undefined;
            }
            // c. Let accumulator be ? IteratorValue(next).
            accumulator = firstResult.Get(CommonProperties.Value);
            counter = 1;
        }
        else
        {
            // 6. Else, let accumulator be initialValue.
            accumulator = arguments.At(1);
        }

        // 7. Repeat,
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            // a. Let value be ? IteratorStepValue(iterated). A throwing "value" getter marks the
            //    record done and propagates; the step is not inside the IfAbruptCloseIterator below.
            // b. If value is done, return accumulator.
            var value = iteratorResult.Get(CommonProperties.Value);

            try
            {
                // c. Set accumulator to ? Call(reducer, undefined, « accumulator, value, 𝔽(counter) »).
                accumulator = reducer.Call(Undefined, [accumulator, value, counter]);
                // d. Set counter to counter + 1.
                counter++;
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }
        }

        return accumulator;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.toarray
    /// </summary>
    [JsFunction]
    private JsValue ToArray(JsValue thisObject)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        var iterated = GetIteratorDirect(o);
        var builder = new JsValueListBuilder(16);
        try
        {
            var iterations = 0;
            while (iterated.TryIteratorStep(out var iteratorResult))
            {
                // `? IteratorStepValue(iterated)`: a throwing "value" getter marks the record done and
                // propagates, so the read stays outside the closing try.
                var value = iteratorResult.Get(CommonProperties.Value);

                try
                {
                    // Check constraints periodically so a huge (or native-backed) iterator
                    // cannot run uninterrupted; the catch closes the iterator.
                    if (++iterations % Engine.ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }

                    builder.Add(value);
                }
                catch
                {
                    iterated.Close(CompletionType.Throw);
                    throw;
                }
            }

            return _realm.Intrinsics.Array.ConstructFromBuilder(ref builder);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.foreach
    /// </summary>
    [JsFunction]
    private JsValue ForEach(JsValue thisObject, JsValue procedure)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        // Validate predicate first, close on failure
        ICallable procedureCallable;
        try
        {
            procedureCallable = GetCallable(procedure);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // Then get the iterator
        var iterated = GetIteratorDirect(o);

        var counter = 0;
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            // `? IteratorStepValue(iterated)`: a throwing "value" getter marks the record done and
            // propagates, so the read stays outside the closing try.
            var value = iteratorResult.Get(CommonProperties.Value);

            try
            {
                procedureCallable.Call(Undefined, [value, counter]);
                counter++;
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }
        }

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.some
    /// </summary>
    [JsFunction]
    private JsValue Some(JsValue thisObject, JsValue predicate)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        // Validate predicate first, close on failure
        ICallable predicateCallable;
        try
        {
            predicateCallable = GetCallable(predicate);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // Then get the iterator
        var iterated = GetIteratorDirect(o);

        var counter = 0;
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            // `? IteratorStepValue(iterated)`: a throwing "value" getter marks the record done and
            // propagates, so the read stays outside the closing try.
            var value = iteratorResult.Get(CommonProperties.Value);

            bool matched;
            try
            {
                var result = predicateCallable.Call(Undefined, [value, counter]);
                matched = TypeConverter.ToBoolean(result);
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }

            if (matched)
            {
                // The closing step is `Return ? IteratorClose(iterated, NormalCompletion(true))`, which
                // sits outside the IfAbruptCloseIterator guarding the predicate call above: "return" is
                // invoked once and whatever it raises stands. Closing from inside that guard would call
                // it a second time.
                iterated.Close(CompletionType.Normal);
                return JsBoolean.True;
            }

            counter++;
        }

        return JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/proposal-iterator-includes/#sec-iterator.prototype.includes
    /// </summary>
    [JsFunction(Length = 1)]
    private JsValue Includes(JsValue thisObject, JsValue searchElement, JsValue skippedElements)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.includes called on non-object");
            return Undefined;
        }

        // 3. Let iterated be the Iterator Record { [[Iterator]]: O, [[NextMethod]]: undefined, [[Done]]: false }.
        //    As in take/drop, the receiver alone stands in for the record: every close below runs
        //    before GetIteratorDirect and so reaches "return" without reading "next".
        // 4. If skippedElements is undefined, let toSkip be 0.
        var toSkip = 0d;
        if (!skippedElements.IsUndefined())
        {
            // 5. Else if skippedElements is not one of +INFINITY, -INFINITY, or an integral Number,
            //    close the iterator and throw a TypeError exception. The value is inspected as-is,
            //    with no ToNumber coercion, which is where NaN lands.
            if (skippedElements is not JsNumber number
                || (!double.IsInfinity(number._value) && !TypeConverter.IsIntegralNumber(number._value)))
            {
                IteratorClose(o, CompletionType.Throw);
                Throw.TypeError(_realm, "skippedElements must be an integral Number or +/-Infinity");
                return Undefined;
            }

            // 6. Else, let toSkip be skippedElements.
            toSkip = number._value;
        }

        // 7. If toSkip < -0, close the iterator and throw a RangeError exception. -0 itself passes,
        //    since -0 < -0 is false; -Infinity does not.
        if (toSkip < 0)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, "skippedElements must not be negative");
            return Undefined;
        }

        // 8. If toSkip is finite and toSkip > 𝔽(2**53 - 1), close the iterator and throw a
        //    RangeError exception. +Infinity is exempt and simply skips every element.
        if (double.IsFinite(toSkip) && toSkip > NumberConstructor.MaxSafeInteger)
        {
            IteratorClose(o, CompletionType.Throw);
            Throw.RangeError(_realm, $"{TypeConverter.ToString(toSkip)} exceeds the maximum safe integer");
            return Undefined;
        }

        // 9. Let skipped be 0.
        var skipped = 0d;

        // 10. Set iterated to ? GetIteratorDirect(O).
        var iterated = GetIteratorDirect(o);

        // 11. Repeat, stepping the iterator until it is exhausted or the search element is found.
        var iterations = 0;
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            // a. Let value be ? IteratorStepValue(iterated). A throwing "value" getter marks the record
            //    done and propagates: the step is not inside the IfAbruptCloseIterator below.
            var value = iteratorResult.Get(CommonProperties.Value);

            bool found;
            try
            {
                if (skipped < toSkip)
                {
                    skipped++;
                    found = false;
                }
                else
                {
                    found = SameValueZeroComparer.Equals(value, searchElement);
                }

                if (!found)
                {
                    // skippedElements may be +Infinity, so this loop is unbounded even for a well
                    // behaved iterator; keep the engine interruptible.
                    if (++iterations % Engine.ConstraintCheckInterval == 0)
                    {
                        _engine.Constraints.Check();
                    }
                }
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }

            if (found)
            {
                // A match closes the iterator; natural exhaustion below deliberately does not. The
                // Repeat's last step is `Return ? IteratorClose(iterated, NormalCompletion(true))`,
                // outside the IfAbruptCloseIterator above, so "return" is invoked exactly once and a
                // throwing one is reported rather than swallowed by a second close.
                iterated.Close(CompletionType.Normal);
                return JsBoolean.True;
            }
        }

        return JsBoolean.False;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.every
    /// </summary>
    [JsFunction]
    private JsValue Every(JsValue thisObject, JsValue predicate)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        // Validate predicate first, close on failure
        ICallable predicateCallable;
        try
        {
            predicateCallable = GetCallable(predicate);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // Then get the iterator
        var iterated = GetIteratorDirect(o);

        var counter = 0;
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            // `? IteratorStepValue(iterated)`: a throwing "value" getter marks the record done and
            // propagates, so the read stays outside the closing try.
            var value = iteratorResult.Get(CommonProperties.Value);

            bool satisfied;
            try
            {
                var result = predicateCallable.Call(Undefined, [value, counter]);
                satisfied = TypeConverter.ToBoolean(result);
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }

            if (!satisfied)
            {
                // See Some: `Return ? IteratorClose(iterated, NormalCompletion(false))` is outside the
                // IfAbruptCloseIterator guard, so "return" runs exactly once.
                iterated.Close(CompletionType.Normal);
                return JsBoolean.False;
            }

            counter++;
        }

        return JsBoolean.True;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype.find
    /// </summary>
    [JsFunction]
    private JsValue Find(JsValue thisObject, JsValue predicate)
    {
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "object must be an Object");
            return Undefined;
        }

        // Validate predicate first, close on failure
        ICallable predicateCallable;
        try
        {
            predicateCallable = GetCallable(predicate);
        }
        catch
        {
            IteratorClose(o, CompletionType.Throw);
            throw;
        }

        // Then get the iterator
        var iterated = GetIteratorDirect(o);

        var counter = 0;
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            // `? IteratorStepValue(iterated)`: a throwing "value" getter marks the record done and
            // propagates, so the read stays outside the closing try.
            var value = iteratorResult.Get(CommonProperties.Value);

            bool matched;
            try
            {
                var result = predicateCallable.Call(Undefined, [value, counter]);
                matched = TypeConverter.ToBoolean(result);
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }

            if (matched)
            {
                // See Some: `Return ? IteratorClose(iterated, NormalCompletion(value))` is outside the
                // IfAbruptCloseIterator guard, so "return" runs exactly once.
                iterated.Close(CompletionType.Normal);
                return value;
            }

            counter++;
        }

        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/proposal-iterator-join/#sec-iterator.prototype.join
    /// </summary>
    [JsFunction]
    private JsValue Join(JsValue thisObject, JsValue separator)
    {
        // 1. Let O be the this value.
        // 2. If O is not an Object, throw a TypeError exception.
        if (thisObject is not ObjectInstance o)
        {
            Throw.TypeError(_realm, "Iterator.prototype.join called on non-object");
            return Undefined;
        }

        string sep;
        if (separator.IsUndefined())
        {
            // 3. If separator is undefined, let sep be ",".
            sep = ",";
        }
        else
        {
            // 4. Else, let sep be Completion(ToString(separator)), closing O if that is abrupt.
            try
            {
                sep = TypeConverter.ToString(separator);
            }
            catch
            {
                IteratorClose(o, CompletionType.Throw);
                throw;
            }
        }

        // 5. Let iterated be ? GetIteratorDirect(O). Reading "next" deliberately happens only after
        // the separator has been coerced, and an abrupt lookup here does NOT close the receiver.
        var iterated = GetIteratorDirect(o);

        // 6. Let R be the empty String. 7. Let first be true.
        using var sb = new ValueStringBuilder();
        var first = true;
        var iterations = 0;

        // 8. Repeat,
        //    a. Let next be ? IteratorStepValue(iterated). b. If next is DONE, return R.
        // An abrupt step — a throwing next(), a non-object result, or a throwing "value" getter —
        // propagates without closing, per IteratorStepValue marking the record done.
        while (iterated.TryIteratorStep(out var iteratorResult))
        {
            var value = iteratorResult.Get(CommonProperties.Value);

            // c. If first is false, set R to the string-concatenation of R and sep.
            if (!first)
            {
                sb.Append(sep);
            }

            // d. Set first to false.
            first = false;

            try
            {
                // e. If next is neither undefined nor null, set R to the string-concatenation of R
                //    and ? ToString(next), closing the iterator if the coercion is abrupt.
                if (!value.IsNullOrUndefined())
                {
                    sb.Append(TypeConverter.ToString(value));
                }

                // Check constraints periodically so a huge (or native-backed) iterator cannot run
                // uninterrupted; the catch closes the iterator.
                if (++iterations % Engine.ConstraintCheckInterval == 0)
                {
                    _engine.Constraints.Check();
                }
            }
            catch
            {
                iterated.Close(CompletionType.Throw);
                throw;
            }
        }

        return sb.Length == 0 ? JsString.Empty : JsString.Create(sb.ToString());
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-iterator.prototype-%symbol.iterator%
    /// </summary>
    [JsSymbolFunction("Iterator", Length = 0, Flags = PropertyFlag.NonEnumerable, CaptureField = nameof(_originalIteratorFunction))]
    private static JsValue ToIterator(JsValue thisObject) => thisObject;

    [JsSymbolFunction("Dispose", Length = 0, Flags = PropertyFlag.NonEnumerable)]
    private static JsValue Dispose(JsValue thisObject)
    {
        var method = thisObject.AsObject().GetMethod(CommonProperties.Return);
        if (method is not null)
        {
            method.Call(thisObject, Arguments.Empty);
        }

        return Undefined;
    }

    [JsFunction(Name = "next")]
    private JsValue NextHandler(JsValue thisObject) => Next(thisObject, Arguments.Empty);

    // Kept with the JsCallArguments signature so still-hand-written subclasses (Array/RegExpString/String
    // IteratorPrototype) can pass it as a ClrFunction delegate from their Initialize bodies.
    internal JsValue Next(JsValue thisObject, JsCallArguments arguments)
    {
        var iterator = thisObject as IteratorInstance;
        if (iterator is null)
        {
            Throw.TypeError(_engine.Realm);
        }

        iterator.TryIteratorStep(out var result);
        return result;
    }
}
