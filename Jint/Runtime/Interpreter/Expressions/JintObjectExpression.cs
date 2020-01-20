using System;
using Esprima.Ast;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;

namespace Jint.Runtime.Interpreter.Expressions
{
    /// <summary>
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-11.1.5
    /// </summary>
    internal sealed class JintObjectExpression : JintExpression
    {
        private JintExpression[] _valueExpressions = ArrayExt.Empty<JintExpression>();
        private ObjectProperty[] _properties = ArrayExt.Empty<ObjectProperty>();

        // check if we can do a shortcut when all are object properties
        // and don't require duplicate checking
        private bool _canBuildFast;

        private class ObjectProperty
        {
            internal readonly string _key;
            internal readonly Property _value;

            public ObjectProperty(string key, Property property)
            {
                _key = key;
                _value = property;
            }
        }

        public JintObjectExpression(Engine engine, ObjectExpression expression) : base(engine, expression)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            _canBuildFast = true;
            var expression = (ObjectExpression) _expression;
            if (expression.Properties.Count == 0)
            {
                // empty object initializer
                return;
            }
            
            _valueExpressions = new JintExpression[expression.Properties.Count];
            _properties = new ObjectProperty[expression.Properties.Count];

            for (var i = 0; i < _properties.Length; i++)
            {
                var property = expression.Properties[i];
                string propName = null;

                if (property.Key is Literal literal)
                {
                    propName = EsprimaExtensions.LiteralKeyToString(literal);
                }

                if (!property.Computed && property.Key is Identifier identifier)
                {
                    propName = identifier.Name;
                }

                _properties[i] = new ObjectProperty(propName, property);

                if (property.Kind == PropertyKind.Init || property.Kind == PropertyKind.Data)
                {
                    var propertyValue = (Expression) property.Value;
                    _valueExpressions[i] = Build(_engine, propertyValue);
                    _canBuildFast &= !propertyValue.IsFunctionWithName();
                }
                else
                {
                    _canBuildFast = false;
                }

                _canBuildFast &= propName != null;
            }
        }

        protected override object EvaluateInternal()
        {
            return _canBuildFast
                ? BuildObjectFast()
                : BuildObjectNormal();
        }

        /// <summary>
        /// Version that can safely build plain object with only normal init/data fields fast.
        /// </summary>
        private object BuildObjectFast()
        {
            var obj = _engine.Object.Construct(0);
            if (_properties.Length == 0)
            {
                return obj;
            }

            var properties = new PropertyDictionary(_properties.Length);
            for (var i = 0; i < _properties.Length; i++)
            {
                var objectProperty = _properties[i];
                var valueExpression = _valueExpressions[i];
                var propValue = valueExpression.GetValue().Clone();
                properties[objectProperty._key] = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
            }
            obj.SetProperties(properties);
            return obj;
        }

        private object BuildObjectNormal()
        {
            var obj = _engine.Object.Construct(Math.Max(2, _properties.Length));
            bool isStrictModeCode = StrictModeScope.IsStrictModeCode;

            for (var i = 0; i < _properties.Length; i++)
            {
                var objectProperty = _properties[i];
                var property = objectProperty._value;
                var propName = objectProperty._key ?? property.GetKey(_engine);

                PropertyDescriptor propDesc;

                if (property.Kind == PropertyKind.Init || property.Kind == PropertyKind.Data)
                {
                    var expr = _valueExpressions[i];
                    var propValue = expr.GetValue().Clone();
                    if (expr._expression.IsFunctionWithName())
                    {
                        var functionInstance = (FunctionInstance) propValue;
                        functionInstance.SetFunctionName(propName);
                    }
                    propDesc = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
                }
                else if (property.Kind == PropertyKind.Get || property.Kind == PropertyKind.Set)
                {
                    var function = property.Value as IFunction ?? ExceptionHelper.ThrowSyntaxError<IFunction>(_engine);

                    var functionInstance = new ScriptFunctionInstance(
                        _engine,
                        function,
                        _engine.ExecutionContext.LexicalEnvironment,
                        isStrictModeCode
                    );
                    functionInstance.SetFunctionName(propName);
                    functionInstance._prototypeDescriptor = null;

                    propDesc = new GetSetPropertyDescriptor(
                        get: property.Kind == PropertyKind.Get ? functionInstance : null,
                        set: property.Kind == PropertyKind.Set ? functionInstance : null,
                        PropertyFlag.Enumerable | PropertyFlag.Configurable);
                }
                else
                {
                    return ExceptionHelper.ThrowArgumentOutOfRangeException<object>();
                }

                obj.DefineOwnProperty(propName, propDesc);
            }

            return obj;
        }
    }
}