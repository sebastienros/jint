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
                baseValue = await thisExpression.GetValueAsync();
            }
            else if (_objectExpression is JintSuperExpression)
            {
                var env = (FunctionEnvironmentRecord)_engine.GetThisEnvironment();
                actualThis = env.GetThisBinding();
                baseValue = env.GetSuperBase();
            }

            if (baseValue is null)
            {
                // fast checks failed
                var baseReference = await _objectExpression.EvaluateAsync();
                if (baseReference is Reference reference)
                {
                    baseReferenceName = reference.GetReferencedName().ToString();
                    baseValue = await _engine.GetValueAsync(reference, false);
                    _engine._referencePool.Return(reference);
                }
                else
                {
                    baseValue = await _engine.GetValueAsync(baseReference, false);
                }
            }

            var property = _determinedProperty ?? await _propertyExpression.GetValueAsync();
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