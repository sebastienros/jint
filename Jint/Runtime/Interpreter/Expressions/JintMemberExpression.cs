using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions;

/// <summary>
/// http://www.ecma-international.org/ecma-262/5.1/#sec-11.2.1
/// </summary>
internal sealed class JintMemberExpression : JintExpression
{
    private readonly MemberExpression _memberExpression;
    private JintExpression _objectExpression = null!;
    private JintExpression? _propertyExpression;
    private JsValue? _determinedProperty;
    private bool _initialized;
    private ObjectInstance? _cachedReadObject;
    private PropertyDescriptor? _cachedReadDescriptor;

    private static readonly JsValue _nullMarker = new JsString("NULL MARKER");

    public JintMemberExpression(MemberExpression expression) : base(expression)
    {
        _memberExpression = (MemberExpression) _expression;
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

    private void EnsureInitialized()
    {
        if (!_initialized)
        {
            _objectExpression = Build(_memberExpression.Object);

            _determinedProperty ??= _expression.UserData as JsValue ?? InitializeDeterminedProperty(_memberExpression, cache: false);

            if (ReferenceEquals(_determinedProperty, _nullMarker))
            {
                _propertyExpression = Build(_memberExpression.Property);
                _determinedProperty = null;
            }

            _initialized = true;
        }
    }

    protected override object EvaluateInternal(EvaluationContext context)
    {
        EnsureInitialized();

        JsValue? actualThis = null;
        object? baseReferenceName = null;
        JsValue? baseValue = null;

        var engine = context.Engine;
        var strict = StrictModeScope.IsStrictModeCode;
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

        var property = _determinedProperty ?? _propertyExpression!.GetValue(context);

        if (property.IsPrivateName())
        {
            return MakePrivateReference(engine, baseValue, property);
        }

        return context.Engine._referencePool.Rent(baseValue, property, StrictModeScope.IsStrictModeCode, thisValue: actualThis);
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
        EnsureInitialized();

        // Fast path for common property reads (e.g. obj.prop) where we can avoid creating and resolving a Reference.
        if (_propertyExpression is null
            && _determinedProperty is JsString determinedProperty
            && !_memberExpression.Optional
            && !_objectExpression._expression.IsOptional()
            && _objectExpression is not JintSuperExpression)
        {
            var baseValue = _objectExpression.GetValue(context);
            if (baseValue is ObjectInstance baseObject)
            {
                context.LastSyntaxElement = _expression;

                if ((baseObject._type & InternalTypes.PlainObject) != InternalTypes.Empty)
                {
                    if (ReferenceEquals(baseObject, _cachedReadObject)
                        && _cachedReadDescriptor is not null)
                    {
                        return ObjectInstance.UnwrapJsValue(_cachedReadDescriptor, baseObject);
                    }

                    var ownDescriptor = baseObject.GetOwnProperty(determinedProperty);
                    if (!ReferenceEquals(ownDescriptor, PropertyDescriptor.Undefined))
                    {
                        if (!ownDescriptor.Configurable)
                        {
                            _cachedReadObject = baseObject;
                            _cachedReadDescriptor = ownDescriptor;
                        }
                        else
                        {
                            _cachedReadObject = null;
                            _cachedReadDescriptor = null;
                        }

                        return ObjectInstance.UnwrapJsValue(ownDescriptor, baseObject);
                    }
                }

                _cachedReadObject = null;
                _cachedReadDescriptor = null;
                return baseObject.Get(determinedProperty, baseObject);
            }
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
            context.Engine._referencePool.Return(reference);
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

            TypeConverter.CheckObjectCoercible(context.Engine, reference.Base, _memberExpression.Property, referenceName);
        }

        return context.Engine.GetValue(reference, returnReferenceToPool: true);
    }
}
