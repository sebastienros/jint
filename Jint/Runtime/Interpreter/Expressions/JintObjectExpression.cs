using System.Collections.Generic;
using System.Threading;
using Esprima.Ast;
using Jint.Collections;
using Jint.Native.Function;
using Jint.Native.Object;
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
            internal string _name;
            internal Property _value;
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
                var propName = property.Key.GetKey();
                _properties[i] = new ObjectProperty
                {
                    _name = propName, _value = property
                };

                if (property.Kind == PropertyKind.Init || property.Kind == PropertyKind.Data)
                {
                    _valueExpressions[i] = Build(_engine, (Expression) property.Value);
                }
                else
                {
                    _canBuildFast = false;
                }

                _canBuildFast &= propertyNames.Add(propName);
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
            var properties = _properties.Length > 1
                ? new StringDictionarySlim<PropertyDescriptor>(_properties.Length)
                : new StringDictionarySlim<PropertyDescriptor>();

            for (var i = 0; i < _properties.Length; i++)
            {
                var objectProperty = _properties[i];
                var propValue = _valueExpressions[i].GetValue();
                properties[objectProperty._name] = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
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
                var propName = objectProperty._name;
                if (!obj._properties.TryGetValue(propName, out var previous))
                {
                    previous = PropertyDescriptor.Undefined;
                }

                PropertyDescriptor propDesc;

                if (property.Kind == PropertyKind.Init || property.Kind == PropertyKind.Data)
                {
                    var expr = _valueExpressions[i];
                    var propValue = expr.GetValue();
                    propDesc = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
                }
                else if (property.Kind == PropertyKind.Get || property.Kind == PropertyKind.Set)
                {
                    var function = property.Value as IFunction ?? ExceptionHelper.ThrowSyntaxError<IFunction>(_engine);

                    ScriptFunctionInstance functionInstance;
                    using (new StrictModeScope(function.Strict))
                    {
                        functionInstance = new ScriptFunctionInstance(
                            _engine,
                            function,
                            _engine.ExecutionContext.LexicalEnvironment,
                            isStrictModeCode
                        );
                    }

                    propDesc = new GetSetPropertyDescriptor(
                        get: property.Kind == PropertyKind.Get ? functionInstance : null,
                        set: property.Kind == PropertyKind.Set ? functionInstance : null,
                        PropertyFlag.Enumerable | PropertyFlag.Configurable);
                }
                else
                {
                    return ExceptionHelper.ThrowArgumentOutOfRangeException<object>();
                }

                if (previous != PropertyDescriptor.Undefined)
                {
                    DefinePropertySlow(isStrictModeCode, previous, propDesc, obj, propName);
                }
                else
                {
                    // do faster direct set
                    obj._properties[propName] = propDesc;
                }
            }

            return obj;
        }

        private void DefinePropertySlow(
            bool isStrictModeCode,
            PropertyDescriptor previous,
            PropertyDescriptor propDesc, ObjectInstance obj, string propName)
        {
            var previousIsDataDescriptor = previous.IsDataDescriptor();
            if (isStrictModeCode && previousIsDataDescriptor && propDesc.IsDataDescriptor())
            {
                ExceptionHelper.ThrowSyntaxError(_engine);
            }

            if (previousIsDataDescriptor && propDesc.IsAccessorDescriptor())
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