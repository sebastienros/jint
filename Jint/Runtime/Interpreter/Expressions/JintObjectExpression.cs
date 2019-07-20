using System;
using System.Collections.Generic;
using System.Threading;
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
        // cache key container for array iteration for less allocations
        private static readonly ThreadLocal<HashSet<string>> _nameDuplicateChecks = new ThreadLocal<HashSet<string>>(() => new HashSet<string>());

        private JintExpression[] _valueExpressions;
        private ObjectProperty[] _properties;

        // check if we can do a shortcut when all are object properties
        // and don't require duplicate checking
        private bool _canBuildFast;

        private class ObjectProperty
        {
            private readonly Key _name;
            internal readonly Property _value;

            public ObjectProperty(in Key propName, Property property)
            {
                _name = propName;
                _value = property;
            }

            public ref readonly Key Name => ref _name;
        }

        public JintObjectExpression(Engine engine, ObjectExpression expression) : base(engine, expression)
        {
            _initialized = false;
        }

        protected override void Initialize()
        {
            var expression = (ObjectExpression) _expression;
            _valueExpressions = new JintExpression[expression.Properties.Count];
            _properties = new ObjectProperty[expression.Properties.Count];

            var propertyNames = _nameDuplicateChecks.Value;
            propertyNames.Clear();

            _canBuildFast = true;
            for (var i = 0; i < _properties.Length; i++)
            {
                var property = expression.Properties[i];
                string propName = null;

                if (property.Key is Literal literal)
                {
                    propName = literal.Value as string ?? Convert.ToString(literal.Value, provider: null);
                }

                if (property.Key is Esprima.Ast.Identifier identifier)
                {
                    propName = identifier.Name;
                }

                _properties[i] = new ObjectProperty(propName ?? "", property);

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

                _canBuildFast &= propertyNames.Add(propName);
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
            var properties = new StringDictionarySlim<PropertyDescriptor>(_properties.Length);

            for (var i = 0; i < _properties.Length; i++)
            {
                var objectProperty = _properties[i];
                var valueExpression = _valueExpressions[i];
                var propValue = valueExpression.GetValue();
                properties[objectProperty.Name] = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
            }

            obj._properties = properties;
            return obj;
        }
                                                
        private object BuildObjectNormal()
        {
            var obj = _engine.Object.Construct(System.Math.Max(2, _properties.Length));
            bool isStrictModeCode = StrictModeScope.IsStrictModeCode;

            for (var i = 0; i < _properties.Length; i++)
            {
                var objectProperty = _properties[i];
                var property = objectProperty._value;
                var propName = !string.IsNullOrEmpty(objectProperty.Name)
                    ? objectProperty.Name
                    : (Key) objectProperty._value.Key.GetKey(_engine);

                PropertyDescriptor propDesc;

                if (property.Kind == PropertyKind.Init || property.Kind == PropertyKind.Data)
                {
                    var expr = _valueExpressions[i];
                    var propValue = expr.GetValue();
                    if (expr._expression.IsFunctionWithName())
                    {
                        var functionInstance = (FunctionInstance) propValue;
                        functionInstance.SetFunctionName(objectProperty.Name);
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
                    functionInstance.SetFunctionName(objectProperty.Name);
                    functionInstance._prototype = null;

                    propDesc = new GetSetPropertyDescriptor(
                        get: property.Kind == PropertyKind.Get ? functionInstance : null,
                        set: property.Kind == PropertyKind.Set ? functionInstance : null,
                        PropertyFlag.Enumerable | PropertyFlag.Configurable);
                }
                else
                {
                    return ExceptionHelper.ThrowArgumentOutOfRangeException<object>();
                }

                obj.DefineOwnProperty(propName, propDesc, false);
            }

            return obj;
        }
    }
}