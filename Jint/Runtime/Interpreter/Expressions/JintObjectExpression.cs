using Esprima.Ast;
using Jint.Native.Function;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Interpreter.Statements;

namespace Jint.Runtime.Interpreter.Expressions
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-11.1.5
    /// </summary>
    internal sealed class JintObjectExpression : JintExpression<ObjectExpression>
    {
        private readonly JintExpression[] _valueExpressions;
        private readonly JintStatement[] _functionStatements;

        public JintObjectExpression(Engine engine, ObjectExpression expression) : base(engine, expression)
        {
            _valueExpressions = new JintExpression[_expression.Properties.Count];
            _functionStatements = new JintStatement[_expression.Properties.Count];
        }

        public override object Evaluate()
        {
            var propertiesCount = _expression.Properties.Count;
            var obj = _engine.Object.Construct(propertiesCount);
            for (var i = 0; i < propertiesCount; i++)
            {
                var property = _expression.Properties[i];
                var propName = property.Key.GetKey();
                if (!obj._properties.TryGetValue(propName, out var previous))
                {
                    previous = PropertyDescriptor.Undefined;
                }

                PropertyDescriptor propDesc;

                if (property.Kind == PropertyKind.Init || property.Kind == PropertyKind.Data)
                {
                    var expr = _valueExpressions[i] ?? (_valueExpressions[i] = Build(_engine, (Expression) property.Value));
                    var exprValue = expr.Evaluate();
                    var propValue = _engine.GetValue(exprValue, true);
                    propDesc = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
                }
                else if (property.Kind == PropertyKind.Get || property.Kind == PropertyKind.Set)
                {
                    var function = property.Value as IFunction;

                    if (function == null)
                    {
                        ExceptionHelper.ThrowSyntaxError(_engine);
                    }

                    ScriptFunctionInstance functionInstance;
                    using (new StrictModeScope(function.Strict))
                    {
                        functionInstance = new ScriptFunctionInstance(
                            _engine,
                            function,
                            _engine.ExecutionContext.LexicalEnvironment,
                            StrictModeScope.IsStrictModeCode
                        );
                    }

                    propDesc = new GetSetPropertyDescriptor(
                        get: property.Kind == PropertyKind.Get ? functionInstance : null,
                        set: property.Kind == PropertyKind.Set ? functionInstance : null,
                        PropertyFlag.Enumerable | PropertyFlag.Configurable);
                }
                else
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException();
                    return null;
                }

                if (previous != PropertyDescriptor.Undefined)
                {
                    if (StrictModeScope.IsStrictModeCode && previous.IsDataDescriptor() && propDesc.IsDataDescriptor())
                    {
                        ExceptionHelper.ThrowSyntaxError(_engine);
                    }

                    if (previous.IsDataDescriptor() && propDesc.IsAccessorDescriptor())
                    {
                        ExceptionHelper.ThrowSyntaxError(_engine);
                    }

                    if (previous.IsAccessorDescriptor() && propDesc.IsDataDescriptor())
                    {
                        ExceptionHelper.ThrowSyntaxError(_engine);
                    }

                    if (previous.IsAccessorDescriptor() && propDesc.IsAccessorDescriptor())
                    {
                        if (!ReferenceEquals(propDesc.Set, null) && !ReferenceEquals(previous.Set, null))
                        {
                            ExceptionHelper.ThrowSyntaxError(_engine);
                        }

                        if (!ReferenceEquals(propDesc.Get, null) && !ReferenceEquals(previous.Get, null))
                        {
                            ExceptionHelper.ThrowSyntaxError(_engine);
                        }
                    }

                    obj.DefineOwnProperty(propName, propDesc, false);
                }
                else
                {
                    // do faster direct set
                    obj._properties[propName] = propDesc;
                }
            }

            return obj;
        }
    }
}