using Jint.Native;
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
    private ObjectInstance? _cachedReadObject;
    private PropertyDescriptor? _cachedReadDescriptor;
    private uint _cachedReadVersion;

    private static readonly JsValue _nullMarker = new JsString("NULL MARKER");

    public JintMemberExpression(MemberExpression expression) : base(expression)
    {
        _memberExpression = (MemberExpression) _expression;
        _objectExpression = Build(_memberExpression.Object);
        _objectExpressionCanShortCircuit = CanShortCircuit(_memberExpression.Object);

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
                }

                _cachedReadObject = null;
                _cachedReadDescriptor = null;
                return baseObject.Get(determinedProperty, baseObject);
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

        var result = Evaluate(context);
        if (result is not Reference reference)
        {
            return (JsValue) result;
        }

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
}
