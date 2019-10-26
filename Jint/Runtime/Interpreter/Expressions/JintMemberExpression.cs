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
        private readonly JintThisExpression _objectThisExpression;

        private readonly JintExpression _propertyExpression;
        private readonly Key _determinedPropertyName;

        public JintMemberExpression(Engine engine, MemberExpression expression) : base(engine, expression)
        {
            _objectExpression = Build(engine, expression.Object);
            _objectIdentifierExpression = _objectExpression as JintIdentifierExpression;
            _objectThisExpression = _objectExpression as JintThisExpression;

            if (!expression.Computed)
            {
                _determinedPropertyName = ((Identifier) expression.Property).Name;
            }
            else
            {
                _determinedPropertyName = "";
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
                baseReferenceName = _objectIdentifierExpression.ExpressionName;
                var strict = isStrictModeCode;
                TryGetIdentifierEnvironmentWithBindingValue(
                    strict,
                    _objectIdentifierExpression.ExpressionName,
                    out _,
                    out baseValue);
            }
            else if (_objectThisExpression != null)
            {
                baseValue = _objectThisExpression.GetValue();
            }

            if (baseValue is null)
            {
                // fast checks failed
                var baseReference = _objectExpression.Evaluate();
                if (baseReference is Reference reference)
                {
                    baseReferenceName = reference.GetReferencedName();
                    baseValue = _engine.GetValue(reference, false);
                    _engine._referencePool.Return(reference);
                }
                else
                {
                    baseValue = _engine.GetValue(baseReference, false);
                }
            }

            var propertyName = _determinedPropertyName.Name.Length > 0
                ? _determinedPropertyName
                : (Key) TypeConverter.ToPropertyKey(_propertyExpression.GetValue());

            TypeConverter.CheckObjectCoercible(_engine, baseValue, (MemberExpression) _expression, baseReferenceName);

            return _engine._referencePool.Rent(baseValue, propertyName, isStrictModeCode);
        }
    }
}