using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-11.2.1
    /// </summary>
    internal sealed class JintMemberExpression : JintExpression
    {
        private readonly JintExpression _objectExpression;
        private readonly JintIdentifierExpression _objectIdentifierExpression;

        private readonly JintExpression _propertyExpression;
        private readonly string _determinedPropertyNameString;

        public JintMemberExpression(Engine engine, MemberExpression expression) : base(engine, expression)
        {
            _objectExpression = Build(engine, expression.Object);
            _objectIdentifierExpression = _objectExpression as JintIdentifierExpression;
            if (!expression.Computed)
            {
                _determinedPropertyNameString = ((Identifier) expression.Property).Name;
            }
            else
            {
                _determinedPropertyNameString = null;
                _propertyExpression = Build(engine, expression.Property);
            }
        }

        protected override object EvaluateInternal()
        {
            string baseReferenceName = null;
            JsValue baseValue = null;
            var isStrictModeCode = StrictModeScope.IsStrictModeCode;

            if (_objectIdentifierExpression != null)
            {
                baseReferenceName = _objectIdentifierExpression._expressionName;
                var strict = isStrictModeCode;
                TryGetIdentifierEnvironmentWithBindingValue(
                    strict,
                    _objectIdentifierExpression._expressionName,
                    out _,
                    out baseValue);
            }

            if (baseValue is null)
            {
                // fast check failed
                var baseReference = _objectExpression.Evaluate();
                if (baseReference is Reference reference)
                {
                    baseReferenceName = reference._name;
                    baseValue = _engine.GetValue(reference, false);
                    _engine._referencePool.Return(reference);
                }
                else
                {
                    baseValue = _engine.GetValue(baseReference, false);
                }
            }

            string propertyNameString = _determinedPropertyNameString;
            if (propertyNameString == null)
            {
                var propertyNameReference = _propertyExpression.Evaluate();
                var propertyNameValue = _engine.GetValue(propertyNameReference, true);
                propertyNameString = TypeConverter.ToPropertyKey(propertyNameValue);
            }

            TypeConverter.CheckObjectCoercible(_engine, baseValue, (MemberExpression) _expression, baseReferenceName);

            return _engine._referencePool.Rent(baseValue, propertyNameString, isStrictModeCode);
        }
    }
}