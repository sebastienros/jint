using Esprima.Ast;
using Jint.Runtime.References;

namespace Jint.Runtime.Interpreter.Expressions
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-11.2.1
    /// </summary>
    internal sealed class JintMemberExpression : JintExpression<MemberExpression>
    {
        public readonly JintExpression _objectExpression;
        private readonly JintExpression _propertyExpression;
        private readonly string _determinedPropertyNameString;

        public JintMemberExpression(Engine engine, MemberExpression expression) : base(engine, expression)
        {
            _objectExpression = Build(engine, expression.Object);
            if (!_expression.Computed)
            {
                _determinedPropertyNameString = ((Identifier) _expression.Property).Name;
            }
            else
            {
                _determinedPropertyNameString = null;
                _propertyExpression = Build(engine, _expression.Property);
            }
        }

        public override object Evaluate()
        {
            var baseReference = _objectExpression.Evaluate();
            var baseValue = _engine.GetValue(baseReference, false);

            string propertyNameString = _determinedPropertyNameString;
            if (propertyNameString == null)
            {
                var propertyNameReference = _propertyExpression.Evaluate();
                var propertyNameValue = _engine.GetValue(propertyNameReference, true);
                propertyNameString = TypeConverter.ToString(propertyNameValue);
            }

            TypeConverter.CheckObjectCoercible(_engine, baseValue, _expression, baseReference);

            if (baseReference is Reference r)
            {
                _engine._referencePool.Return(r);
            }

            return _engine._referencePool.Rent(baseValue, propertyNameString, StrictModeScope.IsStrictModeCode);
        }
    }
}