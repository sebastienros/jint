#nullable enable

using Esprima;
using Esprima.Ast;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;
using Jint.Runtime.Interpreter.Expressions;

namespace Jint.Native.Function
{
    internal class ClassDefinition
    {
        internal readonly string? _className;
        private readonly Expression? _superClass;
        private readonly ClassBody _body;

        public ClassDefinition(
            string? className,
            Expression? superClass, 
            ClassBody body)
        {
            _className = className;
            _superClass = superClass;
            _body = body;
        }
        
        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-classdefinitionevaluation
        /// </summary>
        public ClassConstructorInstance BuildConstructor(
            Engine engine,
            LexicalEnvironment env)
        {
            var classScope = LexicalEnvironment.NewDeclarativeEnvironment(engine, env);

            if (_className is not null)
            {
                classScope._record.CreateImmutableBinding(_className, true);
            }

            ObjectInstance? protoParent = null;
            JsValue? constructorParent = null;
            if (_superClass is null)
            {
                protoParent = engine.Object.PrototypeObject;
                constructorParent = engine.Function.PrototypeObject;
            }
            else
            {
                engine.UpdateLexicalEnvironment(classScope);
                var superclass = JintExpression.Build(engine, _superClass).GetValue();
                engine.UpdateLexicalEnvironment(env);

                if (superclass.IsNull())
                {
                    protoParent = null;
                    constructorParent = engine.Function.PrototypeObject;
                }
                else if (!superclass.IsConstructor)
                {
                    ExceptionHelper.ThrowTypeError(engine, "super class is not a constructor");
                }
                else
                {
                    var temp = superclass.Get("prototype");
                    if (temp is ObjectInstance protoParentObject)
                    {
                        protoParent = protoParentObject;
                    }
                    else if (temp._type == InternalTypes.Null)
                    {
                        // OK
                    }
                    else
                    {
                        ExceptionHelper.ThrowTypeError(engine);
                        return null!;
                    }

                    constructorParent = superclass;
                }
            }

            MethodDefinition? constructor = null;
            var classBody = _body.Body;
            for (var i = 0; i < classBody.Count; ++i)
            {
                if (classBody[i].Kind == PropertyKind.Constructor)
                {
                    constructor = (MethodDefinition) classBody[i];
                    break;
                }
            }

            if (constructor is null)
            {
                string constructorText;
                if (_superClass != null)
                {
                    constructorText = "class temp { constructor(...args) { super(...args); } }";
                }
                else
                {
                    constructorText = "class temp { constructor() {} }";
                }

                var parser = new JavaScriptParser(constructorText, Engine.DefaultParserOptions);
                var script = parser.ParseScript();
                constructor = (MethodDefinition) script.Body[0].ChildNodes[2].ChildNodes[0];
            }

            var proto = engine.Object.Construct(Arguments.Empty);
            proto._prototype = protoParent;

            var functionExpression = (FunctionExpression) constructor.Value;
            var constructorFunction = new JintFunctionDefinition(engine, functionExpression);
            var F = new ClassConstructorInstance(engine, constructorFunction, env, _className)
            {
                // TODO fix logic
                _prototype = constructorParent as ObjectInstance ?? engine.Function.PrototypeObject,
                //var temp = ;
                _prototypeDescriptor = new PropertyDescriptor(proto, PropertyFlag.AllForbidden),
                _length = PropertyDescriptor.AllForbiddenDescriptor.ForNumber(functionExpression.Params.Count),
                _constructorKind = _superClass is null ? ConstructorKind.Base : ConstructorKind.Derived
            };

            engine.UpdateLexicalEnvironment(classScope);

            try
            {
                var newDesc = new PropertyDescriptor(F, PropertyFlag.NonEnumerable);
                proto.DefineOwnProperty(CommonProperties.Constructor, newDesc);
                
                foreach (var classProperty in _body.Body)
                {
                    if (classProperty is not MethodDefinition m)
                    {
                        continue;
                    }

                    var target = !m.Static ? proto : F;
                    var property = TypeConverter.ToPropertyKey(m.GetKey(engine));
                    var valueExpression = JintExpression.Build(engine, m.Value);
                    PropertyDefinitionEvaluation(
                        target,
                        m, 
                        property, 
                        valueExpression, 
                        isStrictModeCode: true);
                }
            }
            finally
            {
                engine.UpdateLexicalEnvironment(env);
            }

            if (_className is not null)
            {
                classScope._record.InitializeBinding(_className, F);
            }

            return F;
        }
        
        private static void PropertyDefinitionEvaluation(
            ObjectInstance obj,
            MethodDefinition property,
            JsValue propName,
            JintExpression valueExpression,
            bool isStrictModeCode)
        {
            PropertyDescriptor? propDesc;
            if (property.Kind == PropertyKind.Get || property.Kind == PropertyKind.Set)
            {
                var function = property.Value as IFunction ?? ExceptionHelper.ThrowSyntaxError<IFunction>(obj.Engine);

                var closure = new ScriptFunctionInstance(
                    obj.Engine,
                    function,
                    obj.Engine.ExecutionContext.LexicalEnvironment,
                    isStrictModeCode
                );
                closure.SetFunctionName(propName);
                closure.MakeMethod(obj);

                propDesc = new GetSetPropertyDescriptor(
                    get: property.Kind == PropertyKind.Get ? closure : null,
                    set: property.Kind == PropertyKind.Set ? closure : null,
                    flags: PropertyFlag.Configurable);
            }
            else
            {
                var expr = valueExpression;
                var propValue = expr.GetValue().Clone();
                if (expr._expression.IsFunctionWithName())
                {
                    var functionInstance = (FunctionInstance) propValue;
                    functionInstance.SetFunctionName(propName);
                }

                propDesc = new PropertyDescriptor(propValue, PropertyFlag.ConfigurableEnumerableWritable);
            }

            obj.DefinePropertyOrThrow(propName, propDesc);
        }
    }
}