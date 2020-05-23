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
        private JintExpression _objectExpression;
        private JintIdentifierExpression _objectIdentifierExpression;
        private JintThisExpression _objectThisExpression;

        private JintExpression _propertyExpression;
        private JsValue _determinedProperty;

        public JintMemberExpression(Engine engine, MemberExpression expression) : base(engine, expression)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            var expression = (MemberExpression) _expression;
            _objectExpression = Build(_engine, expression.Object);
            _objectIdentifierExpression = _objectExpression as JintIdentifierExpression;
            _objectThisExpression = _objectExpression as JintThisExpression;

            if (!expression.Computed)
            {
                _determinedProperty = ((Identifier) expression.Property).Name;
            }
            else if (expression.Property.Type == Nodes.Literal)
            {
                _determinedProperty = JintLiteralExpression.ConvertToJsValue((Literal) expression.Property);
            }

            if (_determinedProperty is null)
            {
                _propertyExpression = Build(_engine, expression.Property);
            }
        }

        protected override object EvaluateInternal()
        {
            string baseReferenceName = null;
            JsValue baseValue = null;
            var isStrictModeCode = StrictModeScope.IsStrictModeCode;

            if (_objectIdentifierExpression != null)
            {
                baseReferenceName = _objectIdentifierExpression._expressionName.Key.Name;
                var strict = isStrictModeCode;
                TryGetIdentifierEnvironmentWithBindingValue(
                    strict,
                    _objectIdentifierExpression._expressionName,
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
            TypeConverter.CheckObjectCoercible(_engine, baseValue, (MemberExpression) _expression, baseReferenceName);
            return _engine._referencePool.Rent(baseValue,  TypeConverter.ToPropertyKey(property), isStrictModeCode);
        }
    }
}