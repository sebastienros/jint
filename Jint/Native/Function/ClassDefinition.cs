#nullable enable

using Esprima;
using Esprima.Ast;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
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
        public ScriptFunctionInstance BuildConstructor(
            Engine engine,
            LexicalEnvironment env)
        {
            var classScope = LexicalEnvironment.NewDeclarativeEnvironment(engine, env);

            if (_className is not null)
            {
                classScope._record.CreateImmutableBinding(_className, true);
            }

            ObjectInstance? protoParent = null;
            ObjectInstance? constructorParent = null;
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

                    constructorParent = (ObjectInstance) superclass;
                }
            }
            
            var proto = new ObjectInstance(engine)
            {
                _prototype = protoParent,
            };

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

            engine.UpdateLexicalEnvironment(classScope);

            ScriptFunctionInstance F;
            try
            {
                var constructorInfo = DefineMethod(engine, constructor, proto, constructorParent);
                F = constructorInfo.Closure;
                if (_className is not null)
                {
                    F.SetFunctionName(_className);
                }
                F.MakeConstructor(false, proto);
                F._constructorKind = _superClass is null ? ConstructorKind.Base : ConstructorKind.Derived;
                F.MakeClassConstructor();
                proto.CreateMethodProperty(CommonProperties.Constructor, F);

                foreach (var classProperty in _body.Body)
                {
                    if (classProperty is not MethodDefinition m || m.Kind == PropertyKind.Constructor)
                    {
                        continue;
                    }

                    var target = !m.Static ? proto : F;
                    PropertyDefinitionEvaluation(engine, target, m);
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
            Engine engine,
            ObjectInstance obj,
            MethodDefinition method)
        {
            if (method.Kind == PropertyKind.Get || method.Kind == PropertyKind.Set)
            {
                var propKey = TypeConverter.ToPropertyKey(method.GetKey(engine));
                var function = method.Value as IFunction ?? ExceptionHelper.ThrowSyntaxError<IFunction>(obj.Engine);

                var closure = new ScriptFunctionInstance(
                    obj.Engine,
                    function,
                    obj.Engine.ExecutionContext.LexicalEnvironment,
                    strict: true
                );
                closure.SetFunctionName(propKey, prefix: method.Kind == PropertyKind.Get ? "get" : "set");
                closure.MakeMethod(obj);

                var propDesc = new GetSetPropertyDescriptor(
                    get: method.Kind == PropertyKind.Get ? closure : null,
                    set: method.Kind == PropertyKind.Set ? closure : null,
                    flags: PropertyFlag.Configurable);

                obj.DefinePropertyOrThrow(propKey, propDesc);
            }
            else
            {
                var methodDef = DefineMethod(engine, method, obj);
                methodDef.Closure.SetFunctionName(methodDef.Key);
                var desc = new PropertyDescriptor(methodDef.Closure, PropertyFlag.NonEnumerable);
                obj.DefinePropertyOrThrow(methodDef.Key, desc);
            }

        }

        private static Record DefineMethod(
            Engine engine,
            MethodDefinition method,
            ObjectInstance obj,
            ObjectInstance? functionPrototype = null)
        {
            var property = TypeConverter.ToPropertyKey(method.GetKey(engine));
            var prototype = functionPrototype ?? engine.Function.PrototypeObject;
            var function = method.Value as IFunction ?? ExceptionHelper.ThrowSyntaxError<IFunction>(engine);

            var closure = new ScriptFunctionInstance(
                engine,
                function,
                engine.ExecutionContext.LexicalEnvironment,
                strict: true,
                proto: prototype);

            closure.MakeMethod(obj);

            return new Record(property, closure);
        }

        private readonly struct Record
        {
            public Record(JsValue key, ScriptFunctionInstance closure)
            {
                Key = key;
                Closure = closure;
            }

            public readonly JsValue Key;
            public readonly ScriptFunctionInstance Closure;
        }
    }
}