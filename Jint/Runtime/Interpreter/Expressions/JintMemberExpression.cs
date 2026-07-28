using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interop;

namespace Jint.Runtime.Interpreter.Expressions;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-11.2.1
/// </summary>
internal sealed class JintMemberExpression : JintExpression
{
    private readonly MemberExpression _memberExpression;
    private readonly JintExpression _objectExpression;
    private readonly JintExpression? _propertyExpression;
    private readonly JsValue? _determinedProperty;
    private readonly bool _objectExpressionCanShortCircuit;
    private readonly bool _computedReadEligible;
    private readonly bool _objectReadKeepsRawEnvironmentWalk;
    private ObjectInstance? _cachedReadObject;
    private PropertyDescriptor? _cachedReadDescriptor;
    private uint _cachedReadVersion;

    // Shape-keyed inline cache for shape-mode receivers (read / member-call / write share these because
    // a member node serves a single role and reads the same property name). A Shape is immutable, so a
    // matching shape reference proves the slot index is still valid — no version check, and the cache
    // hits across *all* objects of the same layout, not just the previously-seen instance.
    private Shape? _cachedShape;
    private int _cachedShapeSlot;

    // Prototype-method inline cache: resolves `obj.method` where `method` lives on obj's direct prototype
    // (e.g. arr.push, date.getTime, obj.protoMethod). The own-property caches above only handle own
    // properties, so without this every such read/call re-walks the prototype chain and probes the
    // prototype's dictionary. Validity: same receiver (so a per-site monomorphic hit), receiver own-shape
    // unchanged (no own property added that would shadow), direct prototype unchanged (not re-pointed),
    // and the holder's own-property shape unchanged (method not redefined/removed). Exotic receivers and
    // prototypes (Proxy/TypedArray/IteratorResult) are excluded via InternalTypes.ExoticGet, and objects
    // whose _propertiesVersion cannot witness this property name are excluded by CanCacheAgainstVersions.
    private ObjectInstance? _cachedProtoReceiver;
    private uint _cachedProtoReceiverVersion;
    private ObjectInstance? _cachedProtoHolder;
    private uint _cachedProtoHolderVersion;
    private PropertyDescriptor? _cachedProtoDescriptor;

    // String-receiver method cache for member calls (str.slice(...)): a primitive string receiver has no
    // own properties beyond `length` and index-coercible names, and those are excluded at build time
    // (_stringReceiverCallEligible), so resolution is prototype-only — the method descriptor is read
    // straight off the realm's %String.prototype% and cached under the same holder-identity +
    // _propertiesVersion guard as the prototype-method cache above. The receiver itself is never boxed
    // or materialized (no ToString/Length touch on lazy CustomString implementations).
    private ObjectInstance? _cachedStringProtoHolder;
    private uint _cachedStringProtoHolderVersion;
    private PropertyDescriptor? _cachedStringProtoDescriptor;
    private readonly bool _stringReceiverCallEligible;

    // Member-call eligibility is a pure function of this node's shape, so it is decided once in the
    // constructor instead of re-deriving four type tests on every call this node is the callee of —
    // JintCallExpression probes it before every member call it dispatches.
    private readonly bool _fastCallEligible;

    // ObjectWrapper member cache: interop receivers carry InternalTypes.ExoticGet (members resolve against
    // the wrapped CLR object), so none of the caches above apply and every host.value read/write walks
    // ObjectWrapper.Get/Set → GetOwnProperty → dictionary probe → reflection accessor. But once
    // ObjectWrapper.GetOwnProperty has resolved a member it stores the descriptor in the wrapper's own
    // _properties and every subsequent Get/Set consults that stored instance first — so receiver identity +
    // _propertiesVersion prove the stored descriptor is still exactly what the wrapper would hand back, and
    // reads/writes can go straight through it. The cached descriptor stays live: a CLR property's
    // ReflectionDescriptor re-invokes the CLR getter/setter on every use, so host-side value changes remain
    // visible and JS-side writes reach the CLR setter. Any define/redefine/delete on the wrapper bumps the
    // version and re-resolves. Population is gated by ObjectWrapper.TryGetInlineCacheableDescriptor (exact
    // ObjectWrapper type only, no dictionary targets, no ICollection `length`, no custom member accessor);
    // _cachedWrapperDescriptor is non-null whenever _cachedWrapper is.
    private ObjectWrapper? _cachedWrapper;
    private uint _cachedWrapperVersion;
    private PropertyDescriptor? _cachedWrapperDescriptor;

    private static readonly JsValue _nullMarker = new JsString("NULL MARKER");

    public JintMemberExpression(MemberExpression expression) : base(expression)
    {
        _memberExpression = (MemberExpression) _expression;
        _objectExpression = Build(_memberExpression.Object);
        _objectExpressionCanShortCircuit = CanShortCircuit(_memberExpression.Object);
        _objectReadKeepsRawEnvironmentWalk = _objectExpression is JintIdentifierExpression { HasEvalOrArguments: true };

        // Computed reads like a[i] / a[i][j] / a[0] (but not super[i] or optional a?.[i]) can take a
        // dense-array fast path in GetValue that resolves base+index without a Reference rent.
        _computedReadEligible = _memberExpression.Computed
            && !_memberExpression.Optional
            && !_objectExpressionCanShortCircuit
            && _objectExpression is not JintSuperExpression;

        var determined = _expression.UserData as JsValue ?? InitializeDeterminedProperty(_memberExpression, cache: false);

        if (ReferenceEquals(determined, _nullMarker))
        {
            _propertyExpression = Build(_memberExpression.Property);
            _determinedProperty = null;
        }
        else
        {
            _determinedProperty = determined;
        }

        _fastCallEligible = _propertyExpression is null
                            && _determinedProperty is JsString
                            && !_memberExpression.Optional
                            && !_objectExpressionCanShortCircuit
                            && _objectExpression is JintIdentifierExpression or JintThisExpression;

        _stringReceiverCallEligible = _fastCallEligible && !CanBeOwnStringInstanceProperty((JsString) _determinedProperty!);
    }

    /// <summary>
    /// Whether a literal property name can resolve to an OWN property of a boxed string: <c>length</c>,
    /// or any name whose <c>ToNumber</c> coercion is a non-negative int32 — <see cref="Native.String.StringInstance"/>.GetOwnProperty
    /// coerces the name, so "0", "01", "0x1", "1e1", " 1", "-0" and even "" can all address a character.
    /// Such names shadow the prototype on string receivers and must never engage the prototype-only
    /// string-method call cache.
    /// </summary>
    private static bool CanBeOwnStringInstanceProperty(JsString name)
    {
        if (CommonProperties.Length.Equals(name))
        {
            return true;
        }

        // Mirrors StringInstance.IsInt32 + the index >= 0 probe; the < length half is receiver-specific,
        // so any non-negative int32 coercion is (conservatively) treated as a possible own index.
        // NaN/Infinity/fractional/negative coercions can never address a character; -0 compares >= 0
        // and is correctly denied.
        var number = TypeConverter.ToNumber(name);
        return number >= 0 && number <= int.MaxValue && (int) number == number;
    }

    /// <summary>
    /// Build-time probe for the comparison lane's member-bound form: a non-computed, non-optional
    /// `.length` read off a plain identifier (`i &lt; arr.length` / `i &lt; s.length`).
    /// </summary>
    internal bool TryGetIdentifierLengthShape([NotNullWhen(true)] out JintIdentifierExpression? baseIdentifier)
    {
        baseIdentifier = null;
        if (_memberExpression.Computed
            || _memberExpression.Optional
            || _objectExpressionCanShortCircuit
            || _objectExpression is not JintIdentifierExpression identifierBase
            || _determinedProperty is not JsString name
            || !string.Equals(name.ToString(), "length", StringComparison.Ordinal))
        {
            return false;
        }

        baseIdentifier = identifierBase;
        return true;
    }

    /// <summary>
    /// Build-time probe for arithmetic-lane leaves: a computed read whose index is an identifier
    /// or a numeric constant. The returned object expression lets the lane compose chains
    /// (`m[i][j]` probes the outer member, then its inner member, down to an identifier base) —
    /// shapes it can later read purely via slot-resolved dense access.
    /// </summary>
    internal bool TryGetComputedIndexShape(
        [NotNullWhen(true)] out JintExpression? objectExpression,
        out JintIdentifierExpression? indexIdentifier,
        out uint constantIndex)
    {
        objectExpression = null;
        indexIdentifier = null;
        constantIndex = 0;

        if (!_computedReadEligible)
        {
            return false;
        }

        if (_determinedProperty is JsNumber determinedNumber
            && ArrayInstance.IsArrayIndex(determinedNumber, out constantIndex))
        {
            objectExpression = _objectExpression;
            return true;
        }

        if (_propertyExpression is JintIdentifierExpression identifierIndex)
        {
            objectExpression = _objectExpression;
            indexIdentifier = identifierIndex;
            return true;
        }

        return false;
    }

    internal static JsValue InitializeDeterminedProperty(MemberExpression expression, bool cache)
    {
        JsValue? property = null;
        if (!expression.Computed)
        {
            if (expression.Property is Identifier identifier)
            {
                property = cache ? JsString.CachedCreate(identifier.Name) : JsString.Create(identifier.Name);
            }
        }
        else if (expression.Property.Type == NodeType.Literal)
        {
            property = JintLiteralExpression.ConvertToJsValue((Literal) expression.Property);
        }

        return property ?? _nullMarker;
    }

    private static bool CanShortCircuit(Expression expression)
    {
        if (expression.IsOptional())
        {
            return true;
        }

        return expression switch
        {
            ChainExpression chainExpression => CanShortCircuit(chainExpression.Expression),
            CallExpression callExpression => CanShortCircuit(callExpression.Callee),
            MemberExpression memberExpression => CanShortCircuit(memberExpression.Object),
            _ => false
        };
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        JsValue? actualThis = null;
        object? baseReferenceName = null;
        JsValue? baseValue = null;

        var engine = context.Engine;
        ref readonly var executionContext = ref engine.ExecutionContext;
        var strict = executionContext.Strict;
        var suspendable = executionContext.Suspendable;

        if (suspendable is { IsResuming: true }
            && suspendable.Data.TryGet(this, out MemberExpressionSuspendData? suspendData))
        {
            // Resume: reuse the previously-resolved object state so a side-effectful
            // object expression (e.g. getObj()[await x]) doesn't run twice.
            baseValue = suspendData!.BaseValue;
            baseReferenceName = suspendData.BaseReferenceName;
            actualThis = suspendData.ActualThis;
        }
        else
        {
            if (_objectExpression is JintIdentifierExpression identifierExpression)
            {
                baseReferenceName = identifierExpression.Identifier.Key.Name;
                if (_objectReadKeepsRawEnvironmentWalk)
                {
                    // `arguments[i]` / `eval.x`: GetValue would run MaterializeIfArguments and
                    // permanently opt the frame's JsArguments out of pooling; the raw walk
                    // returns the live object and mapped-index reads stay on the parameter map.
                    var env = engine.ExecutionContext.LexicalEnvironment;
                    JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                        env,
                        identifierExpression.Identifier,
                        strict,
                        out _,
                        out baseValue);
                }
                else
                {
                    // Route through the identifier node's slot/global caches instead of walking
                    // the environment chain on every evaluation; unresolvable and TDZ bases
                    // throw the same ReferenceError the generic fallback would produce.
                    baseValue = identifierExpression.GetValue(context);
                }
            }
            else if (_objectExpression is JintThisExpression thisExpression)
            {
                baseValue = (JsValue?) thisExpression.GetValue(context);
            }
            else if (_objectExpression is JintSuperExpression)
            {
                var env = (FunctionEnvironment) engine.ExecutionContext.GetThisEnvironment();
                actualThis = env.GetThisBinding();
                baseValue = env.GetSuperBase();
            }

            if (baseValue is null)
            {
                // fast checks failed
                var baseReference = _objectExpression.Evaluate(context);
                if (context.IsSuspended())
                {
                    // The object-side expression itself suspended (e.g. it's a call
                    // expression with an awaiting argument). Do NOT save suspend data:
                    // on resume we re-evaluate _objectExpression so it produces the
                    // real result via its own resume mechanism. Returning a sentinel
                    // Reference here matches previous behavior; the caller's IsSuspended
                    // check bails before use.
                    return context.Engine._referencePool.Rent(JsValue.Undefined, JsValue.Undefined, strict, thisValue: null);
                }
                if (ReferenceEquals(JsValue.Undefined, baseReference))
                {
                    return JsValue.Undefined;
                }
                if (baseReference is Reference reference)
                {
                    baseReferenceName = reference.ReferencedName;
                    baseValue = engine.GetValue(reference, returnReferenceToPool: true);
                }
                else
                {
                    baseValue = engine.GetValue(baseReference, returnReferenceToPool: false);
                }
            }

            if (baseValue.IsNullOrUndefined() && (_memberExpression.Optional || _objectExpression._expression.IsOptional()))
            {
                return JsValue.Undefined;
            }
        }

        var property = _determinedProperty ?? _propertyExpression!.GetValue(context);

        if (context.IsSuspended())
        {
            // Property-side suspended. Save the resolved object state so resume
            // doesn't re-evaluate the (potentially side-effectful) object side.
            if (suspendable is not null)
            {
                var data = suspendable.Data.GetOrCreate<MemberExpressionSuspendData>(this);
                data.BaseValue = baseValue!;
                data.BaseReferenceName = baseReferenceName;
                data.ActualThis = actualThis;
            }
        }
        else
        {
            suspendable?.Data.Clear(this);
        }

        if (property.IsPrivateName())
        {
            return MakePrivateReference(engine, baseValue!, property);
        }

        return context.Engine._referencePool.Rent(baseValue!, property, strict, thisValue: actualThis);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-makeprivatereference
    /// </summary>
    private static Reference MakePrivateReference(Engine engine, JsValue baseValue, JsValue privateIdentifier)
    {
        var privEnv = engine.ExecutionContext.PrivateEnvironment;
        var privateName = privEnv!.ResolvePrivateIdentifier(privateIdentifier.ToString());
        return engine._referencePool.Rent(baseValue, privateName!, strict: true, thisValue: null);
    }

    /// <summary>
    /// Override GetValue to provide proper error location when base is null/undefined.
    /// For read operations, the error should be thrown with the property node's location.
    /// </summary>
    public override JsValue GetValue(EvaluationContext context)
    {
        // Fast path for common property reads (e.g. obj.prop) where we can avoid creating and resolving a Reference.
        var engine = context.Engine;
        if (_propertyExpression is null
            && _determinedProperty is JsString determinedProperty
            && !_memberExpression.Optional
            && !_objectExpressionCanShortCircuit
            && !engine._resolverWatchesValueBase
            && _objectExpression is not JintSuperExpression)
        {
            var baseValue = _objectExpression.GetValue(context);
            if (context.IsSuspended())
            {
                return JsValue.Undefined;
            }

            if (baseValue.IsNullOrUndefined())
            {
                if (engine._nullishPropagatesInline)
                {
                    // The recognized NullPropagatingReferenceResolver yields the base itself for a nullish
                    // base. Serving it here is what the whole recognition is for: no Reference rent, no
                    // interface call, and no error message built only to be discarded. Returning a value is
                    // mandatory rather than merely faster — merely suppressing the throw would fall through to
                    // GetV, whose ToObject(null) raises a different and worse error.
                    return baseValue;
                }

                if (engine._resolverWatchesNullishBase)
                {
                    // A resolver subscribed to null/undefined bases can substitute a value for this read
                    // (the null-propagation use case), and only Engine.GetValue's TryPropertyReference lane
                    // offers it that chance. Complete through a Reference rented from the already-resolved
                    // base — the base is not re-evaluated, so nothing observable happens twice.
                    var nullishBaseReference = engine._referencePool.Rent(baseValue, determinedProperty, engine.ExecutionContext.Strict, thisValue: null);
                    return CompleteReadFromReference(context, engine, nullishBaseReference);
                }

                TypeConverter.CheckObjectCoercible(engine, baseValue, _memberExpression.Property, determinedProperty.ToString());
            }

            context.LastSyntaxElement = _expression;

            if (baseValue.IsObject())
            {
                var baseObject = baseValue.AsObjectNoTypeCheck();
                // One AND against the pair, then two compares against it. Testing the pair rather than
                // ShapeMode alone is what keeps the plain arm below identical to what it was before lazy
                // layout slots existed: an object with no lazy slots takes the same single AND+CMP it always
                // did, and only an object that can actually hold an unmaterialized slot pays the checked read.
                var shapeFlags = baseObject._type & (InternalTypes.ShapeMode | InternalTypes.HasLazySlots);
                if (shapeFlags == InternalTypes.ShapeMode)
                {
                    // Shape-keyed read: a matching shape proves the slot index; read it straight out of
                    // the slot array (no descriptor, no dictionary). Misses re-resolve the slot for the
                    // new shape; an own-property miss (inherited / absent) falls to the full Get.
                    var shapeObj = Unsafe.As<JsObject>(baseObject);
                    var shape = shapeObj.ShapeOf;
                    if (ReferenceEquals(shape, _cachedShape))
                    {
                        return shapeObj.GetSlot(_cachedShapeSlot);
                    }

                    if (shape.TryGetSlot(determinedProperty.ToString(), out var slot))
                    {
                        _cachedShape = shape;
                        _cachedShapeSlot = slot;
                        return shapeObj.GetSlot(slot);
                    }

                    // Slot miss ⇒ no own string property (any non-shapeable property would have deopted).
                    return ReadAfterOwnMiss(baseObject, determinedProperty, ownMissConfirmed: true);
                }

                if (shapeFlags == (InternalTypes.ShapeMode | InternalTypes.HasLazySlots))
                {
                    // Same cache, same slot indices, one checked read: a slot still holding an
                    // unmaterialized lazy layout slot runs its factory here and memoizes into the slot.
                    // _cachedShape/_cachedShapeSlot are deliberately SHARED with the plain arm: a shape maps
                    // names to slots identically whatever the object's slots currently hold — a lazy layout's
                    // shape is the very shape an object literal of the same keys interns — and an object
                    // carrying the flag can never enter the plain arm, so a hit resolved by either arm is
                    // valid for the other. Once every lazy slot is materialized the flag clears and the
                    // object is served by the plain arm, indistinguishable from a literal.
                    var lazyObj = Unsafe.As<JsObject>(baseObject);
                    var lazyShape = lazyObj.ShapeOf;
                    if (ReferenceEquals(lazyShape, _cachedShape))
                    {
                        return lazyObj.GetSlotForRead(_cachedShapeSlot);
                    }

                    if (lazyShape.TryGetSlot(determinedProperty.ToString(), out var lazySlot))
                    {
                        _cachedShape = lazyShape;
                        _cachedShapeSlot = lazySlot;
                        return lazyObj.GetSlotForRead(lazySlot);
                    }

                    return ReadAfterOwnMiss(baseObject, determinedProperty, ownMissConfirmed: true);
                }

                if ((baseObject._type & (InternalTypes.PlainObject | InternalTypes.BuiltinShapeMode)) != InternalTypes.Empty)
                {
                    // Version-based inline cache: as long as the same object is read and its own-property
                    // shape (descriptor add/replace/remove) hasn't changed since we cached, the previously
                    // resolved descriptor reference is still valid — even for configurable properties.
                    if (ReferenceEquals(baseObject, _cachedReadObject)
                        && baseObject._propertiesVersion == _cachedReadVersion
                        && _cachedReadDescriptor is not null)
                    {
                        return ObjectInstance.UnwrapJsValue(_cachedReadDescriptor, baseObject);
                    }

                    var ownDescriptor = baseObject.GetOwnProperty(determinedProperty);
                    if (!ReferenceEquals(ownDescriptor, PropertyDescriptor.Undefined))
                    {
                        _cachedReadObject = baseObject;
                        _cachedReadVersion = baseObject._propertiesVersion;
                        _cachedReadDescriptor = ownDescriptor;

                        return ObjectInstance.UnwrapJsValue(ownDescriptor, baseObject);
                    }

                    // GetOwnProperty already proved the own-property miss for this receiver.
                    _cachedReadObject = null;
                    _cachedReadDescriptor = null;
                    return ReadAfterOwnMiss(baseObject, determinedProperty, ownMissConfirmed: true);
                }

                _cachedReadObject = null;
                _cachedReadDescriptor = null;
                return ReadFromNonPlainReceiver(baseObject, determinedProperty);
            }

            // JsString primitive: skip ToObject's StringInstance allocation for the hot `s.length`
            // case. `for (var i = 0; i < data.length; i++)` re-reads .length every iteration —
            // observed in dromaeo-string-base64 / sunspider string-base64.
            //
            // Other paths fall through to GetV (which allocates a wrapper but is correct) — the
            // wrapper is needed for spec-compliant numeric-index lookup like t['0'] returning
            // the indexed char via StringInstance.GetOwnProperty's ToNumber coercion. v4.8.0
            // allocated for these too via Engine.GetValue's ToObject path.
            if (baseValue.IsString() && CommonProperties.Length.Equals(determinedProperty))
            {
                return JsNumber.Create((uint) baseValue.AsStringNoTypeCheck().Length);
            }

            return baseValue.GetV(engine.Realm, determinedProperty);
        }

        // Fast path for computed dense-array element reads (a[i], a[i][j], a[0]) in non-suspendable
        // contexts: resolve the base (recursively via GetValue, so chained reads stay rent-free) and
        // the index once, then read the dense slot directly. A miss rents a Reference from the
        // already-resolved operands — no re-evaluation — and completes through the normal path. The
        // Suspendable==null gate means neither operand can suspend, so no suspend bookkeeping is needed.
        // The dense-array hit below only fires for an object (JsArray) base; every other outcome —
        // including a null/undefined base — completes through the rented Reference, which still reaches
        // CheckCoercible and TryPropertyReference. So only a resolver watching non-nullish bases has to
        // disarm this lane.
        if (_computedReadEligible
            && !engine._resolverWatchesValueBase
            && engine.ExecutionContext.Suspendable is null)
        {
            var baseValue = _objectExpression.GetValue(context);
            var property = _determinedProperty ?? _propertyExpression!.GetValue(context);
            context.LastSyntaxElement = _expression;

            if (baseValue is JsArray fastArray
                && fastArray.CanUseFastAccess
                && property is JsNumber fastIndexNumber
                && ArrayInstance.IsArrayIndex(fastIndexNumber, out var fastIndex)
                && fastArray.TryGetValueFast(fastIndex, out var fastValue))
            {
                return fastValue;
            }

            // Same lane for a host array-like: one virtual TryGetIndex, no Reference and no key object. Sits
            // after the JsArray hit so array traffic is untouched. A false is an authoritative own miss and
            // falls through to the Reference path below, which keeps prototype-resolved indices and
            // out-of-range reads on the full pipeline.
            if (baseValue is ArrayLikeObject fastArrayLike
                && property is JsNumber arrayLikeIndexNumber
                && ArrayInstance.IsArrayIndex(arrayLikeIndexNumber, out var arrayLikeIndex)
                && fastArrayLike.ReadIndex(arrayLikeIndex, out var arrayLikeValue))
            {
                return arrayLikeValue;
            }

            var rentedReference = engine._referencePool.Rent(baseValue, property, engine.ExecutionContext.Strict, thisValue: null);
            return CompleteReadFromReference(context, engine, rentedReference);
        }

        var result = Evaluate(context);
        if (result is not Reference reference)
        {
            // see JintExpression.GetValue: not a Reference means the protocol guarantees a JsValue
            return Unsafe.As<JsValue>(result);
        }

        return CompleteReadFromReference(context, engine, reference);
    }

    /// <summary>
    /// Completes a read from an already-resolved <see cref="Reference"/>: string-character and
    /// dense-array element fast paths, then the null-base coercibility check, then the full
    /// <see cref="Engine.GetValue(Reference, bool)"/> pipeline. The reference is always returned to
    /// the pool.
    /// </summary>
    private JsValue CompleteReadFromReference(EvaluationContext context, Engine engine, Reference reference)
    {
        // Fast path for string character access: str[intIndex]
        if (_memberExpression.Computed
            && reference.Base is JsString str
            && reference.ReferencedName is JsNumber num
            && num.IsInteger())
        {
            engine._referencePool.Return(reference);
            var index = num.AsInteger();
            if ((uint) index < (uint) str.Length)
            {
                return JsString.Create(str[index]);
            }

            return JsValue.Undefined;
        }

        // Fast path for dense array element access: arr[intIndex] with a clean prototype chain.
        // Skips Engine.GetValue's property pipeline; holes / out-of-range / sparse arrays fall
        // through (TryGetValueFast returns false) so prototype-chain and length semantics are kept.
        if (_memberExpression.Computed
            && reference.Base is JsArray array
            && array.CanUseFastAccess
            && reference.ReferencedName is JsNumber arrayIndexNumber
            && ArrayInstance.IsArrayIndex(arrayIndexNumber, out var arrayIndex)
            && array.TryGetValueFast(arrayIndex, out var arrayValue))
        {
            engine._referencePool.Return(reference);
            return arrayValue;
        }

        // Host array-like element access, reached when the read arrived here with an already-rented Reference
        // (the non-computed-eligible shapes: optional chaining, super bases, short-circuitable object
        // expressions). Same authoritative-miss rule as the branch in GetValue — a false keeps the read on the
        // full pipeline below.
        if (_memberExpression.Computed
            && reference.Base is ArrayLikeObject arrayLike
            && reference.ReferencedName is JsNumber arrayLikeIndexNumber
            && ArrayInstance.IsArrayIndex(arrayLikeIndexNumber, out var arrayLikeIndex)
            && arrayLike.ReadIndex(arrayLikeIndex, out var arrayLikeValue))
        {
            engine._referencePool.Return(reference);
            return arrayLikeValue;
        }

        // Check if base is null/undefined before calling Engine.GetValue
        // This ensures the error has the correct location (the property access)
        // Per ECMAScript spec, ToObject(base) must happen before ToPropertyKey(property),
        // so we must NOT try to convert property to string for the error message if it's an object.
        // The recognized NullPropagatingReferenceResolver is excluded: the read is completed by
        // Engine.GetValue's matching branch, which returns the nullish base, so nothing may throw on the way
        // there — and the error message this block would build is never used. (Were it reached anyway, the
        // resolver's own CheckCoercible accepts a nullish value, so it still would not throw.)
        if (reference.Base.IsNullOrUndefined() && !engine._nullishPropagatesInline)
        {
            var property = reference.ReferencedName;
            // Only use property for error message if it's already a primitive (won't trigger ToPropertyKey)
            var referenceName = property.IsPrimitive()
                ? TypeConverter.ToString(property)
                : null;

            TypeConverter.CheckObjectCoercible(engine, reference.Base, _memberExpression.Property, referenceName);
        }

        return engine.GetValue(reference, returnReferenceToPool: true);
    }

    /// <summary>
    /// Whether this member expression can serve as a call's callee via <see cref="GetCalleeForCall"/>
    /// without renting a <see cref="Reference"/>: a non-computed, non-optional literal-name property
    /// access on a side-effect-free, never-suspending base (a plain identifier or <c>this</c>). The
    /// identifier/<c>this</c> restriction guarantees the base evaluates once with no observable side
    /// effect, so the call's slow-path fallback (taken when the resolved value is not callable) never
    /// double-evaluates anything observable.
    /// </summary>
    internal bool IsFastCallEligible => _fastCallEligible;

    /// <summary>
    /// member call when the receiver is an object, reusing the same version-gated own-property inline
    /// cache as <see cref="GetValue"/> and avoiding a <see cref="Reference"/> rent. <paramref name="thisObject"/>
    /// is the receiver value, matching the property-reference this-binding the slow path produces
    /// (<see cref="Reference.ThisValue"/> is the base). A primitive string receiver resolves the method
    /// prototype-only via the string-method cache when the name was proven at build time to never be an
    /// own property of a boxed string; other primitive receivers (and denied/missed string lookups)
    /// return <see cref="JsValue.Undefined"/> so the caller falls through to the Reference path (which
    /// never forces lazy-string materialization).
    /// </summary>
    internal JsValue GetCalleeForCall(EvaluationContext context, out JsValue thisObject)
    {
        var determinedProperty = (JsString) _determinedProperty!;

        var baseValue = _objectExpression.GetValue(context);
        if (context.IsSuspended())
        {
            thisObject = JsValue.Undefined;
            return JsValue.Undefined;
        }

        context.LastSyntaxElement = _expression;

        // Object receivers take the own-property/prototype cache path below; primitive string receivers
        // take the prototype-only string-method cache further down. Other primitive receivers
        // (number/boolean/...) return undefined here so the caller falls through to the Reference path.
        // The identifier/`this` receiver is side-effect-free, so re-evaluating it on that path is
        // unobservable.
        if (baseValue.IsObject())
        {
            var baseObject = baseValue.AsObjectNoTypeCheck();
            thisObject = baseObject;

            // See the read lane: testing the pair keeps the plain arm byte-equivalent, and the sibling arm
            // below serves the objects that can hold an unmaterialized lazy layout slot.
            var shapeFlags = baseObject._type & (InternalTypes.ShapeMode | InternalTypes.HasLazySlots);
            if (shapeFlags == InternalTypes.ShapeMode)
            {
                var shapeObj = Unsafe.As<JsObject>(baseObject);
                var shape = shapeObj.ShapeOf;
                if (ReferenceEquals(shape, _cachedShape))
                {
                    return shapeObj.GetSlot(_cachedShapeSlot);
                }

                if (shape.TryGetSlot(determinedProperty.ToString(), out var slot))
                {
                    _cachedShape = shape;
                    _cachedShapeSlot = slot;
                    return shapeObj.GetSlot(slot);
                }

                // Slot miss ⇒ no own string property (any non-shapeable property would have deopted).
                return ReadAfterOwnMiss(baseObject, determinedProperty, ownMissConfirmed: true);
            }

            if (shapeFlags == (InternalTypes.ShapeMode | InternalTypes.HasLazySlots))
            {
                // A member call whose callee is a lazy member (`e.body.toString()` resolves the base through
                // the read lane; `e.decode()` resolves the callee here) materializes it like any other value
                // observation. Cache fields shared with the plain arm — see the read lane's note.
                var lazyObj = Unsafe.As<JsObject>(baseObject);
                var lazyShape = lazyObj.ShapeOf;
                if (ReferenceEquals(lazyShape, _cachedShape))
                {
                    return lazyObj.GetSlotForRead(_cachedShapeSlot);
                }

                if (lazyShape.TryGetSlot(determinedProperty.ToString(), out var lazySlot))
                {
                    _cachedShape = lazyShape;
                    _cachedShapeSlot = lazySlot;
                    return lazyObj.GetSlotForRead(lazySlot);
                }

                return ReadAfterOwnMiss(baseObject, determinedProperty, ownMissConfirmed: true);
            }

            if ((baseObject._type & (InternalTypes.PlainObject | InternalTypes.BuiltinShapeMode)) != InternalTypes.Empty)
            {
                if (ReferenceEquals(baseObject, _cachedReadObject)
                    && baseObject._propertiesVersion == _cachedReadVersion
                    && _cachedReadDescriptor is not null)
                {
                    return ObjectInstance.UnwrapJsValue(_cachedReadDescriptor, baseObject);
                }

                var ownDescriptor = baseObject.GetOwnProperty(determinedProperty);
                if (!ReferenceEquals(ownDescriptor, PropertyDescriptor.Undefined))
                {
                    _cachedReadObject = baseObject;
                    _cachedReadVersion = baseObject._propertiesVersion;
                    _cachedReadDescriptor = ownDescriptor;
                    return ObjectInstance.UnwrapJsValue(ownDescriptor, baseObject);
                }

                // GetOwnProperty already proved the own-property miss for this receiver.
                _cachedReadObject = null;
                _cachedReadDescriptor = null;
                return ReadAfterOwnMiss(baseObject, determinedProperty, ownMissConfirmed: true);
            }

            return ReadFromNonPlainReceiver(baseObject, determinedProperty);
        }

        if (_stringReceiverCallEligible && baseValue.IsString())
        {
            var jsString = baseValue.AsStringNoTypeCheck();

            // Primitive-string member call (str.slice(...)): the name can never be an own property of a
            // boxed string (build-time proof), so resolve straight off the realm's %String.prototype% —
            // the same object and receiver-binding Engine.GetValue's string lane uses — guarded by
            // holder identity + _propertiesVersion, exactly like the prototype-method cache above.
            // The receiver is passed through untouched (no boxing, no materialization); `this` is the
            // primitive itself, matching Reference.ThisValue on the slow path. In-place method
            // replacement (String.prototype.slice = fn) mutates the cached descriptor's value and is
            // picked up by UnwrapJsValue; define/delete bump the version and re-resolve. A miss on the
            // direct prototype (absent, or found deeper like Object.prototype.hasOwnProperty) and
            // accessor-backed slots fall back to the Reference path so getter side effects run exactly
            // once even when the result is non-callable.
            thisObject = jsString;

            var stringPrototype = context.Engine.Realm.Intrinsics.String.PrototypeObject;
            if (ReferenceEquals(stringPrototype, _cachedStringProtoHolder)
                && stringPrototype._propertiesVersion == _cachedStringProtoHolderVersion)
            {
                return ObjectInstance.UnwrapJsValue(_cachedStringProtoDescriptor!, jsString);
            }

            var descriptor = stringPrototype.GetOwnProperty(determinedProperty);
            if (!ReferenceEquals(descriptor, PropertyDescriptor.Undefined)
                && (descriptor._flags & PropertyFlag.NonData) == PropertyFlag.None)
            {
                _cachedStringProtoHolder = stringPrototype;
                _cachedStringProtoHolderVersion = stringPrototype._propertiesVersion;
                _cachedStringProtoDescriptor = descriptor;
                return ObjectInstance.UnwrapJsValue(descriptor, jsString);
            }
        }

        thisObject = JsValue.Undefined;
        return JsValue.Undefined;
    }

    /// <summary>
    /// Read completion for receivers outside the shape / plain-object lanes. A host object resolved to
    /// <see cref="PropertyAccessSemantics.Ordinary"/> resolves from a single own-property probe, which also
    /// re-establishes the own miss the prototype-method cache needs — or, when the host overrides
    /// <see cref="ObjectInstance.TryGetOwnPropertyValue"/>, from that hook instead, which answers the same
    /// question and produces the same value with no descriptor. Otherwise an
    /// <see cref="ObjectWrapper"/> receiver consults the wrapper member cache: on a hit (same wrapper instance,
    /// unchanged <c>_propertiesVersion</c>) the stored descriptor is still exactly what <c>ObjectWrapper.Get</c>'s
    /// own-property probe would return, so it unwraps directly — for a CLR property that re-invokes the CLR
    /// getter through the live <c>ReflectionDescriptor</c>, identical to the full path. Everything else —
    /// and any wrapper bail — funnels into <see cref="ReadAfterOwnMiss"/>, whose full <c>Get</c> resolves
    /// and stores the wrapper member so the next populate attempt succeeds.
    /// </summary>
    private JsValue ReadFromNonPlainReceiver(ObjectInstance baseObject, JsString property)
    {
        if ((baseObject._type & InternalTypes.OrdinaryGet) != InternalTypes.Empty)
        {
            // A host-defined object with ordinary [[Get]]. Its own properties live in the host, not in the
            // engine's property bag, so nothing moves _propertiesVersion when that set changes and the version
            // cannot stand in for "this receiver still has no own property of this name" — a projected member
            // that appears after a prototype read was cached must shadow it from the very next read.
            //
            // So the own-property question is asked again on every read, and the prototype-method cache is
            // consulted only once it has been answered "no". That order is also the whole reason such a
            // receiver may be cached at all: this is the only lane that reaches the cache with one, and it
            // re-establishes the own miss before every consult (see CanCacheAgainstVersions).
            //
            // A host that overrides TryGetOwnPropertyValue answers it without a descriptor — one that projects
            // from native storage would otherwise allocate one per read purely for UnwrapJsValue to discard.
            // The hook answers the *same* question, so the ordering above is untouched: a false is an
            // authoritative own miss, exactly what the discarded descriptor would have proved, re-established
            // on this read like every other.
            if ((baseObject._type & InternalTypes.OwnValueHook) != InternalTypes.Empty)
            {
                if (baseObject.TryGetOwnPropertyValue(property, baseObject, out var projectedValue))
                {
                    ObjectInstance.AssertOwnValueAgreesWithDescriptor(baseObject, property, baseObject, answered: true, projectedValue);
                    AssertOrdinaryGetAgrees(baseObject, property, projectedValue);
                    return projectedValue;
                }

                ObjectInstance.AssertOwnValueAgreesWithDescriptor(baseObject, property, baseObject, answered: false, JsValue.Undefined);

                var projectedMiss = ReadAfterOwnMiss(baseObject, property, ownMissConfirmed: true);
                AssertOrdinaryGetAgrees(baseObject, property, projectedMiss);
                return projectedMiss;
            }

            var ownDescriptor = baseObject.GetOwnProperty(property);
            if (!ReferenceEquals(ownDescriptor, PropertyDescriptor.Undefined))
            {
                // The probe that proves the own property exists *is* the read: the descriptor no longer has to
                // be materialized a second time inside Get (ObjectInstance.Get's fast path needs PlainObject,
                // which such an object cannot claim because it stores nothing in _properties).
                var ownValue = ObjectInstance.UnwrapJsValue(ownDescriptor, baseObject);
                AssertOrdinaryGetAgrees(baseObject, property, ownValue);
                return ownValue;
            }

            var inheritedValue = ReadAfterOwnMiss(baseObject, property, ownMissConfirmed: true);
            AssertOrdinaryGetAgrees(baseObject, property, inheritedValue);
            return inheritedValue;
        }

        if (ReferenceEquals(baseObject, _cachedWrapper)
            && baseObject._propertiesVersion == _cachedWrapperVersion)
        {
            return ObjectInstance.UnwrapJsValue(_cachedWrapperDescriptor!, baseObject);
        }

        if (baseObject is ObjectWrapper wrapper)
        {
            var descriptor = wrapper.TryGetInlineCacheableDescriptor(property);
            if (descriptor is not null)
            {
                _cachedWrapper = wrapper;
                _cachedWrapperVersion = wrapper._propertiesVersion;
                _cachedWrapperDescriptor = descriptor;
                return ObjectInstance.UnwrapJsValue(descriptor, wrapper);
            }
        }

        return ReadAfterOwnMiss(baseObject, property, ownMissConfirmed: false);
    }

    /// <summary>
    /// Debug-only verifier for the <see cref="PropertyAccessSemantics.Ordinary"/> contract a host object
    /// declares but Jint cannot check statically: the value the descriptor-driven lane produced must equal
    /// what the object's own <c>Get</c> returns. Free in Release ([Conditional]), so an integration suite run
    /// against a Debug build of Jint becomes the checker. The recomputation is skipped whenever it could be
    /// observable — an accessor, a custom-valued descriptor, or an exotic holder anywhere on the chain — so
    /// enabling it never changes what a script sees.
    /// </summary>
    [Conditional("DEBUG")]
    private static void AssertOrdinaryGetAgrees(ObjectInstance baseObject, JsString property, JsValue value)
    {
#if DEBUG
        for (var o = (ObjectInstance?) baseObject; o is not null; o = o.GetPrototypeOf())
        {
            if ((o._type & InternalTypes.ExoticGet) != InternalTypes.Empty)
            {
                return;
            }

            var descriptor = o.GetOwnProperty(property);
            if (ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
            {
                continue;
            }

            if ((descriptor._flags & (PropertyFlag.NonData | PropertyFlag.CustomJsValue)) != PropertyFlag.None)
            {
                return;
            }

            break;
        }

        Debug.Assert(
            JsValue.SameValue(baseObject.Get(property, baseObject), value),
            $"{baseObject.GetType()} declared PropertyAccessSemantics.Ordinary but its Get('{property}') disagrees with UnwrapJsValue(GetOwnProperty('{property}')). Declare PropertyAccessSemantics.Exotic instead, or make Get ordinary.");
#endif
    }

    /// <summary>
    /// Resolves a member read from <paramref name="baseObject"/> after the own-property fast paths have
    /// missed: tries the prototype-method inline cache, then falls back to the full
    /// <see cref="ObjectInstance.Get(JsValue, JsValue)"/> (deeper prototype chains, exotic objects, absent).
    /// <paramref name="ownMissConfirmed"/> is <c>true</c> when the caller already proved the receiver has no
    /// own property of this name (a shape slot miss or a <c>GetOwnProperty</c> that returned undefined), so
    /// the populate path can skip re-probing it.
    /// </summary>
    private JsValue ReadAfterOwnMiss(ObjectInstance baseObject, JsString property, bool ownMissConfirmed)
    {
        var holder = _cachedProtoHolder;
        if (holder is not null && TryReadFromPrototypeCache(baseObject, holder, out var value))
        {
            return value;
        }

        return ReadAfterOwnMissUncached(baseObject, property, ownMissConfirmed);
    }

    /// <summary>
    /// The prototype-method inline cache's validity check, split out so the ordinary-semantics lane can consult
    /// it after probing the receiver. <paramref name="holder"/> is the already-loaded <c>_cachedProtoHolder</c>,
    /// non-null. Both version comparisons are only meaningful because
    /// <see cref="CanCacheAgainstVersions"/> refused to create an entry whose versions cannot witness the name.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadFromPrototypeCache(ObjectInstance baseObject, ObjectInstance holder, out JsValue value)
    {
        if (ReferenceEquals(baseObject, _cachedProtoReceiver)
            && baseObject._propertiesVersion == _cachedProtoReceiverVersion
            // GetPrototypeOf(), not the _prototype field: a subclass may shadow the field and override
            // [[GetPrototypeOf]] (e.g. interop instances), and base Get walks via the same accessor. The
            // receiver is pinned by identity, so this is a pure field read for ordinary objects and never
            // the proxy trap (proxies carry ExoticGet and are never cached as the receiver).
            && ReferenceEquals(baseObject.GetPrototypeOf(), holder)
            && holder._propertiesVersion == _cachedProtoHolderVersion)
        {
            value = ObjectInstance.UnwrapJsValue(_cachedProtoDescriptor!, baseObject);
            return true;
        }

        value = JsValue.Undefined;
        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private JsValue ReadAfterOwnMissUncached(ObjectInstance baseObject, JsString property, bool ownMissConfirmed)
    {
        // Only ordinary receivers with an ordinary direct prototype: a Proxy / TypedArray / IteratorResult
        // has a custom [[Get]] / [[GetOwnProperty]] this cache must not bypass.
        if ((baseObject._type & InternalTypes.ExoticGet) == InternalTypes.Empty)
        {
            var proto = baseObject.GetPrototypeOf();
            if (proto is not null
                && (proto._type & InternalTypes.ExoticGet) == InternalTypes.Empty
                // No own property on the receiver (which would shadow the prototype's). The caller usually
                // already established this (shape slot miss / GetOwnProperty undefined), so re-probe only
                // when it didn't (a non-plain receiver reached here unchecked).
                && (ownMissConfirmed || baseObject.ProbeOwnProperty(property) == OwnPropertyProbe.Missing))
            {
                // ...and an own property on the *direct* prototype (deeper chains fall to the slow Get).
                var descriptor = proto.GetOwnProperty(property);
                if (!ReferenceEquals(descriptor, PropertyDescriptor.Undefined)
                    && CanCacheAgainstVersions(baseObject, proto, property))
                {
                    _cachedProtoReceiver = baseObject;
                    _cachedProtoReceiverVersion = baseObject._propertiesVersion;
                    _cachedProtoHolder = proto;
                    _cachedProtoHolderVersion = proto._propertiesVersion;
                    _cachedProtoDescriptor = descriptor;
                    return ObjectInstance.UnwrapJsValue(descriptor, baseObject);
                }
            }
        }

        return baseObject.Get(property, baseObject);
    }

    /// <summary>
    /// Whether an entry may be created for this (receiver, holder, name) triple — that is, whether the two
    /// <c>_propertiesVersion</c> comparisons <see cref="TryReadFromPrototypeCache"/> makes can still prove, on a
    /// later read, that the name is absent from the receiver and owned by the holder.
    /// <para>
    /// A version only witnesses the own properties the engine stores itself. Two kinds of object keep some of
    /// theirs elsewhere and move no version when that part of the set changes: an <b>array</b>, whose elements
    /// live in its own dense/sparse storage and are written straight there by the hot element paths, and a
    /// <b>host-defined subclass</b> with ordinary reads, whose whole own-property set lives outside the engine.
    /// So an array must not be validated by its version for an index-like name, and a host object must not be
    /// validated by its version for any name.
    /// </para>
    /// <para>
    /// The receiver side has one exemption: <see cref="ReadFromNonPlainReceiver"/> is the only lane that reaches
    /// this cache with a host receiver, and it establishes the own miss anew before every consult — with a real
    /// <c>GetOwnProperty</c> probe, or with a <c>false</c> from
    /// <see cref="ObjectInstance.TryGetOwnPropertyValue"/>, which states the same thing — so that receiver's
    /// frozen version is never load-bearing. A holder has no such lane and must be witnessed outright.
    /// </para>
    /// </summary>
    private static bool CanCacheAgainstVersions(ObjectInstance receiver, ObjectInstance holder, JsString property)
    {
        var receiverIsProvable = (receiver._type & InternalTypes.OrdinaryGet) != InternalTypes.Empty
                                 || VersionWitnessesOwnProperty(receiver, property);

        return receiverIsProvable && VersionWitnessesOwnProperty(holder, property);
    }

    /// <summary>
    /// Whether <paramref name="o"/>'s <c>_propertiesVersion</c> moves when a property named
    /// <paramref name="property"/> joins or leaves its own-property set. See <see cref="CanCacheAgainstVersions"/>
    /// for the two storage kinds it does not cover.
    /// <para>
    /// The <see cref="InternalTypes.OrdinaryGet"/> refusal is about <em>where an object's own properties
    /// live</em>, not about how it reads them. The flag is derived from the .NET type — a subclass reaching
    /// the protected <c>ObjectInstance(Engine)</c> constructor gets it — and for a host subclass it correctly
    /// stands in for "this object's own-property set is outside the engine, so no engine-side counter can
    /// witness it". A <see cref="InternalTypes.BuiltinShapeMode"/> object is the one case where that
    /// inference is wrong, so it is carved out. Two facts make the carve-out sound, and it is only sound
    /// while both hold:
    /// </para>
    /// <para>
    /// <b>1. Its own-property set is entirely engine storage, and every change to that set bumps the
    /// version.</b> The names are the shared layout's slots plus the hybrid side dictionary. Redefining a
    /// declared slot (<c>SetProperty</c> / <c>SetOwnProperty</c>), adding a name
    /// (<c>TryHybridAddToShapedHost</c>), removing one (<c>RemoveOwnProperty</c>) and falling back to the
    /// dictionary (<c>DeoptBuiltinShape</c>, through <c>SetProperties</c>) each bump
    /// <c>_propertiesVersion</c>. Lazily materializing a slot deliberately does not — but materialization
    /// does not change the name set: the name was already an own property, only its descriptor was pending.
    /// So the version witnesses exactly the question this method asks.
    /// </para>
    /// <para>
    /// <b>2. No host type can carry the flag.</b> It is set only by <c>ObjectInstance.InitializeBuiltinShape</c>,
    /// which is <c>private protected</c>, over the internal <c>IBuiltinShaped</c> storage protocol; both
    /// <c>BuiltinShapeObject</c> and the object <c>JsObjectShape.Instantiate</c> returns are internal. A
    /// third-party subclass therefore cannot reach builtin-shape mode, and the refusal this carve-out relaxes
    /// stays in force for every object it was written for — the host subclasses whose properties the engine
    /// genuinely cannot see. The array clause below is untouched and still applies to a shaped array-backed
    /// holder such as <c>Array.prototype</c>, whose elements do live outside the counter.
    /// </para>
    /// </summary>
    private static bool VersionWitnessesOwnProperty(ObjectInstance o, JsString property)
    {
        if ((o._type & InternalTypes.OrdinaryGet) != InternalTypes.Empty
            && (o._type & InternalTypes.BuiltinShapeMode) == InternalTypes.Empty)
        {
            return false;
        }

        return (o._type & InternalTypes.Array) == InternalTypes.Empty
               || !ArrayInstance.IsArrayIndex(property, out _);
    }

    /// <summary>
    /// Write-side counterpart of <see cref="GetValue"/>'s inline cache for <c>obj.prop = rhs</c>. Reuses the
    /// same version-gated own-property cache slots: when the receiver is a <see cref="InternalTypes.PlainObject"/>
    /// whose shape is unchanged and the own property is a <em>live</em> writable, non-accessor, non-custom data
    /// descriptor, the new value is written straight into the descriptor (no Reference rent, no property-key hash,
    /// no dictionary lookup) — exactly the in-place store <see cref="ObjectInstance.Set(JsValue,JsValue,JsValue)"/>
    /// performs, which by design does not bump <c>_propertiesVersion</c>.
    /// <para>
    /// The method returns <c>false</c> only from the eligibility gate, having evaluated nothing, so the caller's
    /// unchanged slow path runs. Once the base and right-hand side have been evaluated (each exactly once, in spec
    /// order) it always completes the assignment and returns <c>true</c>: either the in-place store, or — for an
    /// absent / accessor / read-only / custom-value property, or a non-<see cref="ObjectInstance"/> base — a
    /// fallback through <see cref="Engine.PutValue"/> rented from the already-resolved base+key (so a side-effecting
    /// base or RHS is never evaluated twice and prototype-setter / CreateDataProperty / strict read-only semantics
    /// are preserved).
    /// </para>
    /// </summary>
    internal bool TryAssignFast(EvaluationContext context, JintExpression right, out JsValue result)
    {
        var engine = context.Engine;

        // Same eligibility as GetValue's primary fast path, minus its custom-resolver gate, plus the
        // computed-read path's Suspendable==null gate: a static string-named, non-optional,
        // non-short-circuiting, non-super property write in a context where neither operand can suspend
        // (so no generator/async bookkeeping is needed).
        //
        // Unlike the read path, this one needs no custom-resolver gate: IReferenceResolver has no write-side
        // member, and none of its four methods is consulted while completing a property store —
        // Engine.PutValue reaches the resolver on none of its branches. The base is still evaluated through
        // the normal read path (which does consult the resolver, including for an unresolvable base), and a
        // nullish base is not an ObjectInstance so it flows to the PutValue fallback and throws there exactly
        // as the slow path would.
        if (_propertyExpression is not null
            || _determinedProperty is not JsString determinedProperty
            || _memberExpression.Optional
            || _objectExpressionCanShortCircuit
            || _objectExpression is JintSuperExpression
            || engine.ExecutionContext.Suspendable is not null)
        {
            result = JsValue.Undefined;
            return false;
        }

        // Evaluate base, then RHS — each exactly once, preserving base→key→rhs spec order. A null/undefined base
        // is simply not a PlainObject and flows to the fallback, where PutValue→ToObject throws after the RHS.
        var baseValue = _objectExpression.GetValue(context);
        var rval = right.GetValue(context);

        context.LastSyntaxElement = _expression;

        if (baseValue.IsObject())
        {
            var baseObject = baseValue.AsObjectNoTypeCheck();
            if ((baseObject._type & InternalTypes.ShapeMode) != InternalTypes.Empty)
            {
                // Shape-keyed write: shape-mode properties are always writable data, so a slot match is
                // an in-place store with no descriptor, no hash, no version bump. An absent own property
                // falls through to PutValue (add / inherited-setter / CreateDataProperty semantics).
                var shapeObj = Unsafe.As<JsObject>(baseObject);
                var shape = shapeObj.ShapeOf;
                int slot;
                if (ReferenceEquals(shape, _cachedShape))
                {
                    slot = _cachedShapeSlot;
                }
                else if (shape.TryGetSlot(determinedProperty.ToString(), out slot))
                {
                    _cachedShape = shape;
                    _cachedShapeSlot = slot;
                }
                else
                {
                    slot = -1;
                }

                if (slot >= 0)
                {
                    shapeObj.SetSlot(slot, rval);
                    result = rval;
                    return true;
                }
            }
            else if ((baseObject._type & (InternalTypes.PlainObject | InternalTypes.BuiltinShapeMode)) != InternalTypes.Empty)
            {
                PropertyDescriptor? descriptor;
                if (ReferenceEquals(baseObject, _cachedReadObject)
                    && baseObject._propertiesVersion == _cachedReadVersion
                    && _cachedReadDescriptor is not null)
                {
                    descriptor = _cachedReadDescriptor;
                }
                else
                {
                    var ownDescriptor = baseObject.GetOwnProperty(determinedProperty);
                    if (ReferenceEquals(ownDescriptor, PropertyDescriptor.Undefined))
                    {
                        // Absent own property: inherited-setter / CreateDataProperty semantics — handled by fallback.
                        _cachedReadObject = null;
                        _cachedReadDescriptor = null;
                        descriptor = null;
                    }
                    else
                    {
                        _cachedReadObject = baseObject;
                        _cachedReadVersion = baseObject._propertiesVersion;
                        _cachedReadDescriptor = ownDescriptor;
                        descriptor = ownDescriptor;
                    }
                }

                // Re-read the flags live every store: Object.defineProperty flips Writable in place on the same
                // descriptor without bumping the version, so the writability decision must never be cached. The mask
                // must equal exactly Writable — i.e. writable, not an accessor (NonData), not custom-valued.
                if (descriptor is not null
                    && (descriptor._flags & (PropertyFlag.NonData | PropertyFlag.CustomJsValue | PropertyFlag.Writable)) == PropertyFlag.Writable)
                {
                    descriptor._value = rval;
                    result = rval;
                    return true;
                }
            }
            else
            {
                // ObjectWrapper member lane: a stored member means ObjectWrapper.Set would route through
                // SetSlow (ContainsKey ⇒ CanPut + `ownDesc.Value = value`), and the receiver-identity +
                // _propertiesVersion guard proves the cached descriptor is that stored instance. Mirror
                // SetSlow exactly: CanPut's own-descriptor branch with live flag/accessor reads
                // (defineProperty can mutate the same instance without a version bump), then store through
                // the descriptor — a ReflectionDescriptor forwards to the CLR setter, keeping conversion
                // and exception semantics. Non-writable members (e.g. read-only CLR properties, whose
                // ReflectionDescriptor exposes no setter when not writable or interop writes are disabled)
                // fall through to the PutValue fallback so strict/sloppy failure behavior stays identical.
                // Population only consumes descriptors a read has already stored; it never resolves members
                // itself, so unstored writes keep ObjectWrapper.Set's accessor fast path untouched.
                PropertyDescriptor? descriptor;
                if (ReferenceEquals(baseObject, _cachedWrapper)
                    && baseObject._propertiesVersion == _cachedWrapperVersion)
                {
                    descriptor = _cachedWrapperDescriptor;
                }
                else if (baseObject is ObjectWrapper wrapper
                    && (descriptor = wrapper.TryGetInlineCacheableDescriptor(determinedProperty)) is not null)
                {
                    _cachedWrapper = wrapper;
                    _cachedWrapperVersion = wrapper._propertiesVersion;
                    _cachedWrapperDescriptor = descriptor;
                }
                else
                {
                    descriptor = null;
                }

                if (descriptor is not null)
                {
                    bool canPut;
                    if (descriptor.IsAccessorDescriptor())
                    {
                        var set = descriptor.Set;
                        canPut = set is not null && !set.IsUndefined();
                    }
                    else
                    {
                        canPut = descriptor.Writable;
                    }

                    if (canPut)
                    {
                        descriptor.Value = rval;
                        result = rval;
                        return true;
                    }
                }
            }
        }

        // Fallback: complete via the normal pipeline from the already-resolved base + key (no re-evaluation).
        var reference = engine._referencePool.Rent(baseValue, determinedProperty, engine.ExecutionContext.Strict, thisValue: null);
        engine.PutValue(reference, rval);
        engine._referencePool.Return(reference);
        result = rval;
        return true;
    }
}
