using Esprima.Ast;
using Jint.Native;
using Jint.Runtime.Environments;
using Jint.Runtime.References;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Expressions
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-11.2.1
    /// </summary>
    internal sealed partial class JintMemberExpression : JintExpression
    {
        protected async override Task<object> EvaluateInternalAsync()
        {
            string baseReferenceName = null;
            JsValue baseValue = null;
            var isStrictModeCode = StrictModeScope.IsStrictModeCode;

            if (_objectIdentifierExpression != null)
            {
                baseReferenceName = _objectIdentifierExpression._expressionName.Key.Name;
                var strict = isStrictModeCode;
                var env = _engine.ExecutionContext.LexicalEnvironment;
                LexicalEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                    env,
                    _objectIdentifierExpression._expressionName,
                    strict,
                    out _,
                    out baseValue);
            }
            else if (_objectThisExpression != null)
            {
                baseValue = await _objectThisExpression.GetValueAsync();
            }

            if (baseValue is null)
            {
                // fast checks failed
                var baseReference = await _objectExpression.EvaluateAsync();
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

            var property = _determinedProperty ?? await _propertyExpression.GetValueAsync();
            TypeConverter.CheckObjectCoercible(_engine, baseValue, (MemberExpression)_expression, _determinedProperty?.ToString() ?? baseReferenceName);
            return _engine._referencePool.Rent(baseValue, TypeConverter.ToPropertyKey(property), isStrictModeCode);
        }
    }
}