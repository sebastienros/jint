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
    internal sealed class ClassDefinition
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
                var script = new JavaScriptParser().ParseScript(source);
                var classDeclaration = (ClassDeclaration) script.Body[0];
                return (MethodDefinition) classDeclaration.Body.Body[0];
            }

            _superConstructor = CreateConstructorMethodDefinition("class temp { constructor(...args) { super(...args); } }");
            _emptyConstructor = CreateConstructorMethodDefinition("class temp { constructor() {} }");
        }

        public ClassDefinition(
            string? className,
            Expression? superClass,
            ClassBody body)
        {
            _className = className;
            _superClass = superClass;
            _body = body;
        }

        public void Initialize()
        {

        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-classdefinitionevaluation
        /// </summary>
        public JsValue BuildConstructor(
            EvaluationContext context,
            EnvironmentRecord env)
        {
            // A class definition is always strict mode code.
            using var _ = new StrictModeScope(true, true);

            var engine = context.Engine;
            var classScope = JintEnvironment.NewDeclarativeEnvironment(engine, env);

            if (_className is not null)
            {
                classScope.CreateImmutableBinding(_className, true);
            }

            var outerPrivateEnvironment = engine.ExecutionContext.PrivateEnvironment;
            var classPrivateEnvironment = JintEnvironment.NewPrivateEnvironment(engine, outerPrivateEnvironment);

            /*
                6. If ClassBodyopt is present, then
                    a. For each String dn of the PrivateBoundIdentifiers of ClassBodyopt, do
                    i. If classPrivateEnvironment.[[Names]] contains a Private Name whose [[Description]] is dn, then
                    1. Assert: This is only possible for getter/setter pairs.
                ii. Else,
                    1. Let name be a new Private Name whose [[Description]] value is dn.
                    2. Append name to classPrivateEnvironment.[[Names]].
             */

            ObjectInstance? protoParent = null;
            ObjectInstance? constructorParent = null;
            if (_superClass is null)
            {
                protoParent = engine.Realm.Intrinsics.Object.PrototypeObject;
                constructorParent = engine.Realm.Intrinsics.Function.PrototypeObject;
            }
            else
            {
                engine.UpdateLexicalEnvironment(classScope);
                var superclass = JintExpression.Build(_superClass).GetValue(context);
                engine.UpdateLexicalEnvironment(env);

                if (superclass.IsNull())
                {
                    protoParent = null;
                    constructorParent = engine.Realm.Intrinsics.Function.PrototypeObject;
                }
                else if (!superclass.IsConstructor)
                {
                    ExceptionHelper.ThrowTypeError(engine.Realm, "super class is not a constructor");
                }
                else
                {
                    var temp = superclass.Get("prototype");
                    if (temp is ObjectInstance protoParentObject)
                    {
                        protoParent = protoParentObject;
                    }
                    else if (temp.IsNull())
                    {
                        // OK
                    }
                    else
                    {
                        ExceptionHelper.ThrowTypeError(engine.Realm, "cannot resolve super class prototype chain");
                        return default;
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
                if (classBody[i] is MethodDefinition { Kind: PropertyKind.Constructor } c)
                {
                    constructor = c;
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

                F.MakeConstructor(writableProperty: false, proto);
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
                    var value = MethodDefinitionEvaluation(engine, target, m);
                    if (engine._activeEvaluationContext!.IsAbrupt())
                    {
                        return value;
                    }
                }
            }
            finally
            {
                engine.UpdateLexicalEnvironment(env);
                engine.UpdatePrivateEnvironment(outerPrivateEnvironment);
            }

            if (_className is not null)
            {
                classScope.InitializeBinding(_className, F);
            }

            /*
            28. Set F.[[PrivateMethods]] to instancePrivateMethods.
            29. Set F.[[Fields]] to instanceFields.
            30. For each PrivateElement method of staticPrivateMethods, do
                a. Perform ! PrivateMethodOrAccessorAdd(method, F).
            31. For each element fieldRecord of staticFields, do
                a. Let result be DefineField(F, fieldRecord).
                b. If result is an abrupt completion, then
            i. Set the running execution context's PrivateEnvironment to outerPrivateEnvironment.
                ii. Return result.
            */

            engine.UpdatePrivateEnvironment(outerPrivateEnvironment);

            return F;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-methoddefinitionevaluation
        /// </summary>
        private static JsValue MethodDefinitionEvaluation(
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
                var value = method.TryGetKey(engine);
                if (engine._activeEvaluationContext!.IsAbrupt())
                {
                    return value;
                }
                var propKey = TypeConverter.ToPropertyKey(value);
                var function = method.Value as IFunction;
                if (function is null)
                {
                    ExceptionHelper.ThrowSyntaxError(obj.Engine.Realm);
                }

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

            return obj;
        }
    }
}
