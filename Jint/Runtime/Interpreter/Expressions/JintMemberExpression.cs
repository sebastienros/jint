using System.Runtime.CompilerServices;
using Jint.Native;
using Jint.Native.Array;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;

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
    // prototypes (Proxy/TypedArray/IteratorResult) are excluded via InternalTypes.ExoticGet.
    private ObjectInstance? _cachedProtoReceiver;
    private uint _cachedProtoReceiverVersion;
    private ObjectInstance? _cachedProtoHolder;
    private uint _cachedProtoHolderVersion;
    private PropertyDescriptor? _cachedProtoDescriptor;

    private static readonly JsValue _nullMarker = new JsString("NULL MARKER");

    public JintMemberExpression(MemberExpression expression) : base(expression)
    {
        _memberExpression = (MemberExpression) _expression;
        _objectExpression = Build(_memberExpression.Object);
        _objectExpressionCanShortCircuit = CanShortCircuit(_memberExpression.Object);

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
        var strict = StrictModeScope.IsStrictModeCode;
        var suspendable = engine.ExecutionContext.Suspendable;

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
                var identifier = identifierExpression.Identifier;
                baseReferenceName = identifier.Key.Name;
                var env = engine.ExecutionContext.LexicalEnvironment;
                JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                    env,
                    identifier,
                    strict,
                    out _,
                    out baseValue);
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
                    return context.Engine._referencePool.Rent(JsValue.Undefined, JsValue.Undefined, StrictModeScope.IsStrictModeCode, thisValue: null);
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

        return context.Engine._referencePool.Rent(baseValue!, property, StrictModeScope.IsStrictModeCode, thisValue: actualThis);
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
            && !engine._customResolver
            && _objectExpression is not JintSuperExpression)
        {
            var baseValue = _objectExpression.GetValue(context);
            if (context.IsSuspended())
            {
                return JsValue.Undefined;
            }

            if (baseValue.IsNullOrUndefined())
            {
                TypeConverter.CheckObjectCoercible(engine, baseValue, _memberExpression.Property, determinedProperty.ToString());
            }

            context.LastSyntaxElement = _expression;

            if (baseValue is ObjectInstance baseObject)
            {
                if ((baseObject._type & InternalTypes.ShapeMode) != InternalTypes.Empty)
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

                if ((baseObject._type & InternalTypes.PlainObject) != InternalTypes.Empty)
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
                return ReadAfterOwnMiss(baseObject, determinedProperty, ownMissConfirmed: false);
            }

            // JsString primitive: skip ToObject's StringInstance allocation for the hot `s.length`
            // case. `for (var i = 0; i < data.length; i++)` re-reads .length every iteration —
            // observed in dromaeo-string-base64 / sunspider string-base64.
            //
            // Other paths fall through to GetV (which allocates a wrapper but is correct) — the
            // wrapper is needed for spec-compliant numeric-index lookup like t['0'] returning
            // the indexed char via StringInstance.GetOwnProperty's ToNumber coercion. v4.8.0
            // allocated for these too via Engine.GetValue's ToObject path.
            if (baseValue is JsString jsString && CommonProperties.Length.Equals(determinedProperty))
            {
                return JsNumber.Create((uint) jsString.Length);
            }

            return baseValue.GetV(engine.Realm, determinedProperty);
        }

        // Fast path for computed dense-array element reads (a[i], a[i][j], a[0]) in non-suspendable
        // contexts: resolve the base (recursively via GetValue, so chained reads stay rent-free) and
        // the index once, then read the dense slot directly. A miss rents a Reference from the
        // already-resolved operands — no re-evaluation — and completes through the normal path. The
        // Suspendable==null gate means neither operand can suspend, so no suspend bookkeeping is needed.
        if (_computedReadEligible
            && !engine._customResolver
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

            var rentedReference = engine._referencePool.Rent(baseValue, property, StrictModeScope.IsStrictModeCode, thisValue: null);
            return CompleteReadFromReference(context, engine, rentedReference);
        }

        var result = Evaluate(context);
        if (result is not Reference reference)
        {
            return (JsValue) result;
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

        // Check if base is null/undefined before calling Engine.GetValue
        // This ensures the error has the correct location (the property access)
        // Per ECMAScript spec, ToObject(base) must happen before ToPropertyKey(property),
        // so we must NOT try to convert property to string for the error message if it's an object.
        if (reference.Base.IsNullOrUndefined())
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
    internal bool IsFastCallEligible
        => _propertyExpression is null
           && _determinedProperty is JsString
           && !_memberExpression.Optional
           && !_objectExpressionCanShortCircuit
           && _objectExpression is JintIdentifierExpression or JintThisExpression;

    /// <summary>
    /// member call when the receiver is an object, reusing the same version-gated own-property inline
    /// cache as <see cref="GetValue"/> and avoiding a <see cref="Reference"/> rent. <paramref name="thisObject"/>
    /// is the receiver object, matching the property-reference this-binding the slow path produces. For a
    /// primitive receiver this returns <see cref="JsValue.Undefined"/> so the caller falls through to the
    /// Reference path (which never forces lazy-string materialization).
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

        // Only object receivers take the fast path. Primitive receivers (string/number/...) — including
        // custom JsString subclasses with lazy materialization — return undefined here so the caller
        // falls through to the Reference path, which resolves them without forcing materialization. The
        // identifier/`this` receiver is side-effect-free, so re-evaluating it on that path is unobservable.
        if (baseValue is ObjectInstance baseObject)
        {
            thisObject = baseObject;

            if ((baseObject._type & InternalTypes.ShapeMode) != InternalTypes.Empty)
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

            if ((baseObject._type & InternalTypes.PlainObject) != InternalTypes.Empty)
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

            return ReadAfterOwnMiss(baseObject, determinedProperty, ownMissConfirmed: false);
        }

        thisObject = JsValue.Undefined;
        return JsValue.Undefined;
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
        if (holder is not null
            && ReferenceEquals(baseObject, _cachedProtoReceiver)
            && baseObject._propertiesVersion == _cachedProtoReceiverVersion
            // GetPrototypeOf(), not the _prototype field: a subclass may shadow the field and override
            // [[GetPrototypeOf]] (e.g. interop instances), and base Get walks via the same accessor. The
            // receiver is pinned by identity, so this is a pure field read for ordinary objects and never
            // the proxy trap (proxies carry ExoticGet and are never cached as the receiver).
            && ReferenceEquals(baseObject.GetPrototypeOf(), holder)
            && holder._propertiesVersion == _cachedProtoHolderVersion)
        {
            return ObjectInstance.UnwrapJsValue(_cachedProtoDescriptor!, baseObject);
        }

        return ReadAfterOwnMissUncached(baseObject, property, ownMissConfirmed);
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
                && (ownMissConfirmed || ReferenceEquals(baseObject.GetOwnProperty(property), PropertyDescriptor.Undefined)))
            {
                // ...and an own property on the *direct* prototype (deeper chains fall to the slow Get).
                var descriptor = proto.GetOwnProperty(property);
                if (!ReferenceEquals(descriptor, PropertyDescriptor.Undefined))
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

        // Same eligibility as GetValue's primary fast path, plus the computed-read path's Suspendable==null gate:
        // a static string-named, non-optional, non-short-circuiting, non-super property write with no custom
        // resolver, in a context where neither operand can suspend (so no generator/async bookkeeping is needed).
        if (_propertyExpression is not null
            || _determinedProperty is not JsString determinedProperty
            || _memberExpression.Optional
            || _objectExpressionCanShortCircuit
            || engine._customResolver
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

        if (baseValue is ObjectInstance baseObject)
        {
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
            else if ((baseObject._type & InternalTypes.PlainObject) != InternalTypes.Empty)
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
        }

        // Fallback: complete via the normal pipeline from the already-resolved base + key (no re-evaluation).
        var reference = engine._referencePool.Rent(baseValue, determinedProperty, StrictModeScope.IsStrictModeCode, thisValue: null);
        engine.PutValue(reference, rval);
        engine._referencePool.Return(reference);
        result = rval;
        return true;
    }
}
