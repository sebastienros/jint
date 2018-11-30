using Esprima.Ast;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Interpreter.Expressions
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-11.1.5
    /// </summary>
    internal sealed class JintObjectExpression : JintExpression<ObjectExpression>
    {
        private JintExpression[] _valueExpressions;
        private ObjectProperty[] _properties;

        private class ObjectProperty
        {
            internal string _name;
            internal Property _value;
        }

        public JintObjectExpression(Engine engine, ObjectExpression expression) : base(engine, expression)
        {
        }

        protected override void Initialize()
        {
            _valueExpressions = new JintExpression[_expression.Properties.Count];
            _properties = new ObjectProperty[_expression.Properties.Count];
            for (var i = 0; i < _properties.Length; i++)
            {
                var property = _expression.Properties[i];
                var propName = property.Key.GetKey();
                _properties[i] = new ObjectProperty
                {
                    _name = propName, _value = property
                };
            }
        }

        protected override object EvaluateInternal()
        {
            var obj = _engine.Object.Construct(_properties.Length);
            for (var i = 0; i < _properties.Length; i++)
            {
                var objectProperty = _properties[i];
                var property = objectProperty._value;
                var propName = objectProperty._name;
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
                    DefinePropertySlow(previous, propDesc, obj, propName);
                }
                else
                {
                    // do faster direct set
                    obj._properties[propName] = propDesc;
                }
            }

            return obj;
        }

        private void DefinePropertySlow(PropertyDescriptor previous, PropertyDescriptor propDesc, ObjectInstance obj, string propName)
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
    }
}