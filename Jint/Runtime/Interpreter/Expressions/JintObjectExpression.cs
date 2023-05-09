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
        private JintExpression[] _valueExpressions = Array.Empty<JintExpression>();
        private ObjectProperty?[] _properties = Array.Empty<ObjectProperty>();

        // check if we can do a shortcut when all are object properties
        // and don't require duplicate checking
        private bool _canBuildFast;

        private sealed class ObjectProperty
        {
            internal readonly string? _key;
            private JsString? _keyJsString;
            internal readonly Property _value;
            private JintFunctionDefinition? _functionDefinition;

            public ObjectProperty(string? key, Property property)
            {
                _key = key;
                _value = property;
            }

            public JsString? KeyJsString => _keyJsString ??= _key != null ? JsString.Create(_key) : null;

            public JintFunctionDefinition GetFunctionDefinition(Engine engine)
            {
                if (_functionDefinition is not null)
                {
                    return _functionDefinition;
                }

                var function = _value.Value as IFunction;
                if (function is null)
                {
                    ExceptionHelper.ThrowSyntaxError(engine.Realm);
                }

                _functionDefinition = new JintFunctionDefinition(function);
                return _functionDefinition;
            }
        }

        private JintObjectExpression(ObjectExpression expression) : base(expression)
        {
            _initialized = false;
        }

        public static JintExpression Build(ObjectExpression expression)
        {
            return expression.Properties.Count == 0
                ? JintEmptyObjectExpression.Instance
                : new JintObjectExpression(expression);
        }

        protected override void Initialize(EvaluationContext context)
        {
            _canBuildFast = true;
            var expression = (ObjectExpression) _expression;
            ref readonly var properties = ref expression.Properties;

            _valueExpressions = new JintExpression[properties.Count];
            _properties = new ObjectProperty[properties.Count];

            for (var i = 0; i < _properties.Length; i++)
            {
                string? propName = null;
                var property = properties[i];
                if (property is Property p)
                {
                    if (p.Key is Literal literal)
                    {
                        propName = EsprimaExtensions.LiteralKeyToString(literal);
                    }

                    if (!p.Computed && p.Key is Identifier identifier)
                    {
                        propName = identifier.Name;
                        _canBuildFast &= propName != "__proto__";
                    }

                    _properties[i] = new ObjectProperty(propName, p);

                    if (p.Kind is PropertyKind.Init)
                    {
                        var propertyValue = p.Value;
                        _valueExpressions[i] = Build((Expression) propertyValue);
                        _canBuildFast &= !propertyValue.IsFunctionDefinition();
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
                    _valueExpressions[i] = Build(spreadElement.Argument);
                }
                else
                {
                    ExceptionHelper.ThrowArgumentOutOfRangeException("property", "cannot handle property " + property);
                }

                _canBuildFast &= propName != null;
            }
        }

        protected override object EvaluateInternal(EvaluationContext context)
        {
            return _canBuildFast
                ? BuildObjectFast(context)
                : BuildObjectNormal(context);
        }

        /// <summary>
        /// Version that can safely build plain object with only normal init/data fields fast.
        /// </summary>
        private object BuildObjectFast(EvaluationContext context)
        {
            var obj = new JsObject(context.Engine);
            var properties = new PropertyDictionary(_properties.Length, checkExistingKeys: true);
            for (var i = 0; i < _properties.Length; i++)
            {
                var objectProperty = _properties[i];
                var valueExpression = _valueExpressions[i];
                var propValue = valueExpression.GetValue(context).Clone();
                properties[objectProperty!._key!] = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
            }

            obj.SetProperties(properties);
            return obj;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-object-initializer-runtime-semantics-propertydefinitionevaluation
        /// </summary>
        private object BuildObjectNormal(EvaluationContext context)
        {
            var engine = context.Engine;
            var obj = engine.Realm.Intrinsics.Object.Construct(_properties.Length);

            for (var i = 0; i < _properties.Length; i++)
            {
                var objectProperty = _properties[i];

                if (objectProperty is null)
                {
                    // spread
                    if (_valueExpressions[i].GetValue(context) is ObjectInstance source)
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

                JsValue? propName = objectProperty.KeyJsString;
                if (propName is null)
                {
                    var value = property.TryGetKey(engine);
                    if (context.IsAbrupt())
                    {
                        return value;
                    }

                    propName = TypeConverter.ToPropertyKey(value);
                }

                if (property.Kind == PropertyKind.Init)
                {
                    var expr = _valueExpressions[i];
                    var completion = expr.GetValue(context);
                    if (context.IsAbrupt())
                    {
                        return completion;
                    }

                    var propValue = completion.Clone();
                    if (objectProperty._key == "__proto__" && !objectProperty._value.Computed && !objectProperty._value.Shorthand)
                    {
                        if (propValue.IsObject() || propValue.IsNull())
                        {
                            obj.SetPrototypeOf(propValue);
                        }
                        continue;
                    }

                    if (expr._expression.IsAnonymousFunctionDefinition())
                    {
                        var closure = (FunctionInstance) propValue;
                        closure.SetFunctionName(propName);
                    }

                    var propDesc = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
                    obj.DefinePropertyOrThrow(propName, propDesc);
                }
                else if (property.Kind == PropertyKind.Get || property.Kind == PropertyKind.Set)
                {
                    var function = objectProperty.GetFunctionDefinition(engine);
                    var closure = engine.Realm.Intrinsics.Function.OrdinaryFunctionCreate(
                        engine.Realm.Intrinsics.Function.PrototypeObject,
                        function,
                        function.ThisMode,
                        engine.ExecutionContext.LexicalEnvironment,
                        engine.ExecutionContext.PrivateEnvironment);

                    closure.SetFunctionName(propName, property.Kind == PropertyKind.Get ? "get" : "set");
                    closure.MakeMethod(obj);

                    var propDesc = new GetSetPropertyDescriptor(
                        get: property.Kind == PropertyKind.Get ? closure : null,
                        set: property.Kind == PropertyKind.Set ? closure : null,
                        PropertyFlag.Enumerable | PropertyFlag.Configurable);

                    obj.DefinePropertyOrThrow(propName, propDesc);
                }
            }

            return obj;
        }

        internal sealed class JintEmptyObjectExpression : JintExpression
        {
            public static JintEmptyObjectExpression Instance = new(new ObjectExpression(NodeList.Create(System.Linq.Enumerable.Empty<Node>())));

            private JintEmptyObjectExpression(Expression expression) : base(expression)
            {
            }

            protected override object EvaluateInternal(EvaluationContext context)
            {
                return new JsObject(context.Engine);
            }

            public override JsValue GetValue(EvaluationContext context)
            {
                return new JsObject(context.Engine);
            }
        }
    }
}


