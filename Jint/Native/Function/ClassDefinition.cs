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
        private static readonly MethodDefinition _superConstructor;
        private static readonly MethodDefinition _emptyConstructor;

        internal readonly string? _className;
        private readonly Expression? _superClass;
        private readonly ClassBody _body;

        static ClassDefinition()
        {
            // generate missing constructor AST only once
            static MethodDefinition CreateConstructorMethodDefinition(string source)
            {
                var parser = new JavaScriptParser(source);
                var script = parser.ParseScript();
                return (MethodDefinition) script.Body[0].ChildNodes[2].ChildNodes[0];
            }

            _superConstructor = CreateConstructorMethodDefinition("class temp { constructor(...args) { super(...args); } }");
            _emptyConstructor = CreateConstructorMethodDefinition("class temp { constructor() {} }");
        }

        public ClassDefinition(
            Identifier? className,
            Expression? superClass,
            ClassBody body)
        {
            _className = className?.Name;
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
            // A class definition is always strict mode code.
            using var _ = (new StrictModeScope(true, true));
            
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
                _prototype = protoParent
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

            constructor ??= _superClass != null
                ? _superConstructor
                : _emptyConstructor;

            engine.UpdateLexicalEnvironment(classScope);

            ScriptFunctionInstance F;
            try
            {
                var constructorInfo = constructor.DefineMethod(proto, constructorParent);
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

        /// <summary>
        /// https://tc39.es/ecma262/#sec-method-definitions-runtime-semantics-propertydefinitionevaluation
        /// </summary>
        private static void PropertyDefinitionEvaluation(
            Engine engine,
            ObjectInstance obj,
            MethodDefinition method)
        {
            if (method.Kind != PropertyKind.Get && method.Kind != PropertyKind.Set)
            {
                var methodDef = method.DefineMethod(obj);
                methodDef.Closure.SetFunctionName(methodDef.Key);
                var desc = new PropertyDescriptor(methodDef.Closure, PropertyFlag.NonEnumerable);
                obj.DefinePropertyOrThrow(methodDef.Key, desc);
            }
            else
            {
                var propKey = TypeConverter.ToPropertyKey(method.GetKey(engine));
                var function = method.Value as IFunction ?? ExceptionHelper.ThrowSyntaxError<IFunction>(obj.Engine);

                var closure = new ScriptFunctionInstance(
                    obj.Engine,
                    function,
                    obj.Engine.ExecutionContext.LexicalEnvironment,
                    true);
                closure.SetFunctionName(propKey, method.Kind == PropertyKind.Get ? "get" : "set");
                closure.MakeMethod(obj);

                var propDesc = new GetSetPropertyDescriptor(
                    method.Kind == PropertyKind.Get ? closure : null,
                    method.Kind == PropertyKind.Set ? closure : null,
                    PropertyFlag.Configurable);

                obj.DefinePropertyOrThrow(propKey, propDesc);
            }
        }
    }
}