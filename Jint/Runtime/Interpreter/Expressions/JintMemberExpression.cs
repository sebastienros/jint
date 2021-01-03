using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-11.2.1
    /// </summary>
    internal sealed class JintMemberExpression : JintExpression
    {
        private MemberExpression _memberExpression;
        private JintExpression _objectExpression;
        private JintExpression _propertyExpression;
        private JsValue _determinedProperty;

        public JintMemberExpression(Engine engine, MemberExpression expression) : base(engine, expression)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            _memberExpression = (MemberExpression) _expression;
            _objectExpression = Build(_engine, _memberExpression.Object);

            if (!_memberExpression.Computed)
            {
                _determinedProperty = ((Identifier) _memberExpression.Property).Name;
            }
            else if (_memberExpression.Property.Type == Nodes.Literal)
            {
                _determinedProperty = JintLiteralExpression.ConvertToJsValue((Literal) _memberExpression.Property);
            }

            if (_determinedProperty is null)
            {
                _propertyExpression = Build(_engine, _memberExpression.Property);
            }
        }

        protected override object EvaluateInternal()
        {
            JsValue actualThis = null;
            string baseReferenceName = null;
            JsValue baseValue = null;
            var isStrictModeCode = StrictModeScope.IsStrictModeCode;

            if (_objectExpression is JintIdentifierExpression identifierExpression)
            {
                baseReferenceName = identifierExpression._expressionName.Key.Name;
                var strict = isStrictModeCode;
                var env = _engine.ExecutionContext.LexicalEnvironment;
                LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                    env,
                    identifierExpression._expressionName,
                    strict,
                    out _,
                    out baseValue);
            }
            else if (_objectExpression is JintThisExpression thisExpression)
            {
                baseValue = thisExpression.GetValue();
            }
            else if (_objectExpression is JintSuperExpression)
            {
                var env = (FunctionEnvironmentRecord) _engine.GetThisEnvironment();
                actualThis = env.GetThisBinding();
                baseValue = env.GetSuperBase();
            }

            if (baseValue is null)
            {
                // fast checks failed
                var baseReference = _objectExpression.Evaluate();
                if (baseReference is Reference reference)
                {
                    baseReferenceName = reference.GetReferencedName().ToString();
                    baseValue = _engine.GetValue(reference, false);
                    _engine._referencePool.Return(reference);
                }
                else
                {
                    baseValue = _engine.GetValue(baseReference, false);
                }
            }

            var property = _determinedProperty ?? _propertyExpression.GetValue();
            if (baseValue.IsNullOrUndefined())
            {
                TypeConverter.CheckObjectCoercible(_engine, baseValue, _memberExpression.Property, _determinedProperty?.ToString() ?? baseReferenceName);
            }

            // only convert if necessary
            var propertyKey = property.IsInteger() && baseValue.IsIntegerIndexedArray
                ? property
                : TypeConverter.ToPropertyKey(property);

            return _engine._referencePool.Rent(baseValue, propertyKey, isStrictModeCode, thisValue: actualThis);
        }
    }
}