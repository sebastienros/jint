#nullable enable

using Esprima.Ast;
using Jint.Collections;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interpreter.Expressions
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-object-initializer
    /// </summary>
    internal sealed class JintObjectExpression : JintExpression
    {
        private JintExpression[] _valueExpressions = System.Array.Empty<JintExpression>();
        private ObjectProperty?[] _properties = System.Array.Empty<ObjectProperty>();

        // check if we can do a shortcut when all are object properties
        // and don't require duplicate checking
        private bool _canBuildFast;

        private class ObjectProperty
        {
            internal readonly string? _key;
            private JsString? _keyJsString;
            internal readonly Property _value;

            public ObjectProperty(string? key, Property property)
            {
                _key = key;
                _value = property;
            }

            public JsString? KeyJsString => _keyJsString ??= _key != null ? JsString.Create(_key) : null;
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
                string? propName = null;
                var property = expression.Properties[i];
                if (property is Property p)
                {
                    if (p.Key is Literal literal)
                    {
                        propName = EsprimaExtensions.LiteralKeyToString(literal);
                    }

                    if (!p.Computed && p.Key is Identifier identifier)
                    {
                        propName = identifier.Name;
                    }

                    _properties[i] = new ObjectProperty(propName, p);

                    if (p.Kind == PropertyKind.Init || p.Kind == PropertyKind.Data)
                    {
                        var propertyValue = p.Value;
                        _valueExpressions[i] = Build(_engine, propertyValue);
                        _canBuildFast &= !propertyValue.IsFunctionWithName();
                    }
                    else
                    {
                        _canBuildFast = false;
                    }
                }
                else if (property is SpreadElement spreadElement)
                {
                    _canBuildFast = false;
                    _properties[i] = null;
                    _valueExpressions[i] = Build(_engine, spreadElement.Argument);
                }
                else
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException("property", "cannot handle property " + property);
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

            var properties = new PropertyDictionary(_properties.Length, checkExistingKeys: true);
            for (var i = 0; i < _properties.Length; i++)
            {
                var objectProperty = _properties[i];
                var valueExpression = _valueExpressions[i];
                var propValue = valueExpression.GetValue().Clone();
                properties[objectProperty!._key] = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
            }
            obj.SetProperties(properties);
            return obj;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object-initializer-runtime-semantics-propertydefinitionevaluation
        /// </summary>
        private object BuildObjectNormal()
        {
            var obj = _engine.Object.Construct(_properties.Length);
            bool isStrictModeCode = _engine._isStrict || StrictModeScope.IsStrictModeCode;

            for (var i = 0; i < _properties.Length; i++)
            {
                var objectProperty = _properties[i];

                if (objectProperty is null)
                {
                    // spread
                    if (_valueExpressions[i].GetValue() is ObjectInstance source)
                    {
                        source.CopyDataProperties(obj, null);
                    }
                    continue;
                }
                
                var property = objectProperty._value;

                if (property.Method)
                {
                    var methodDef = property.DefineMethod(obj);
                    methodDef.Closure.SetFunctionName(methodDef.Key);
                    var desc = new PropertyDescriptor(methodDef.Closure, PropertyFlag.ConfigurableEnumerableWritable);
                    obj.DefinePropertyOrThrow(methodDef.Key, desc);
                    continue;
                }
                
                var propName = objectProperty.KeyJsString ?? property.GetKey(_engine);
                if (property.Kind == PropertyKind.Init || property.Kind == PropertyKind.Data)
                {
                    var expr = _valueExpressions[i];
                    JsValue propValue = expr.GetValue().Clone();
                    if (expr._expression.IsFunctionWithName())
                    {
                        var closure = (FunctionInstance) propValue;
                        closure.SetFunctionName(propName);
                    }

                    var propDesc = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
                    obj.DefinePropertyOrThrow(propName, propDesc);
                }
                else if (property.Kind == PropertyKind.Get || property.Kind == PropertyKind.Set)
                {
                    var function = property.Value as IFunction ?? ExceptionHelper.ThrowSyntaxError<IFunction>(_engine);

                    var closure = new ScriptFunctionInstance(
                        _engine,
                        function,
                        _engine.ExecutionContext.LexicalEnvironment,
                        isStrictModeCode);
                    closure.SetFunctionName(propName, property.Kind == PropertyKind.Get ? "get" : "set");

                    var propDesc = new GetSetPropertyDescriptor(
                        get: property.Kind == PropertyKind.Get ? closure : null,
                        set: property.Kind == PropertyKind.Set ? closure : null,
                        PropertyFlag.Enumerable | PropertyFlag.Configurable);
                    
                    obj.DefinePropertyOrThrow(propName, propDesc);
                }
            }

            return obj;
        }
    }
}