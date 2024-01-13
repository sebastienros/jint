using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions
{
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
            else if (expression.Property.Type == Nodes.Literal)
            {
                property = JintLiteralExpression.ConvertToJsValue((Literal) expression.Property);
            }

            return property ?? _nullMarker;
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            if (!_initialized)
            {
                _objectExpression = Build(_memberExpression.Object);

                _determinedProperty ??= _expression.AssociatedData as JsValue ?? InitializeDeterminedProperty(_memberExpression, cache: false);

                if (ReferenceEquals(_determinedProperty, _nullMarker))
                {
                    _propertyExpression = Build(_memberExpression.Property);
                    _determinedProperty = null;
                }

                _initialized = true;
            }

            JsValue? actualThis = null;
            string? baseReferenceName = null;
            JsValue? baseValue = null;
            var isStrictModeCode = StrictModeScope.IsStrictModeCode;

            var engine = context.Engine;
            if (_objectExpression is JintIdentifierExpression identifierExpression)
            {
                var identifier = identifierExpression.Identifier;
                baseReferenceName = identifier.Key.Name;
                var strict = isStrictModeCode;
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
                    baseReferenceName = reference.ReferencedName.ToString();
                    baseValue = engine.GetValue(reference, false);
                    engine._referencePool.Return(reference);
                }
                else
                {
                    baseValue = engine.GetValue(baseReference, false);
                }
            }

            if (baseValue.IsNullOrUndefined() && (_memberExpression.Optional || _objectExpression._expression.IsOptional()))
            {
                return JsValue.Undefined;
            }

            var property = _determinedProperty ?? _propertyExpression!.GetValue(context);
            if (baseValue.IsNullOrUndefined())
            {
                // we can use base data types securely, object evaluation can mess things up
                var referenceName = property.IsPrimitive()
                    ? TypeConverter.ToString(property)
                    : _determinedProperty?.ToString() ?? baseReferenceName;

                TypeConverter.CheckObjectCoercible(engine, baseValue, _memberExpression.Property, referenceName!);
            }

            if (property.IsPrivateName())
            {
                return MakePrivateReference(engine, baseValue, property);
            }

            // only convert if necessary
            var propertyKey = property.IsInteger() && baseValue.IsIntegerIndexedArray
                ? property
                : TypeConverter.ToPropertyKey(property);

            return context.Engine._referencePool.Rent(baseValue, propertyKey, isStrictModeCode, thisValue: actualThis);
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
    }
}
