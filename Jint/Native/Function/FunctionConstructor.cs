using Esprima;
using Esprima.Ast;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Function
{
    /// <summary>
    /// https://tc39.es/ecma262/#sec-function-constructor
    /// </summary>
    public sealed class FunctionConstructor : FunctionInstance, IConstructor
    {
        private static readonly ParserOptions ParserOptions = new ParserOptions { Tolerant = false };
        private static readonly JsString _functionName = new JsString("Function");
        private static readonly JsString _functionNameAnonymous = new JsString("anonymous");

        internal FunctionConstructor(
            Engine engine,
            Realm realm,
            ObjectPrototype objectPrototype)
            : base(engine, realm, _functionName)
        {
            PrototypeObject = new FunctionPrototype(engine, realm, objectPrototype);
            _prototype = PrototypeObject;
            _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
            _length = new PropertyDescriptor(JsNumber.PositiveOne, PropertyFlag.Configurable);
        }

        public FunctionPrototype PrototypeObject { get; }

        protected internal override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments, thisObject);
        }

        ObjectInstance IConstructor.Construct(JsValue[] arguments, JsValue newTarget) => Construct(arguments, newTarget);

        private ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var function = CreateDynamicFunction(
                this,
                newTarget,
                FunctionKind.Normal,
                arguments);

            return function;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-createdynamicfunction
        /// </summary>
        internal FunctionInstance CreateDynamicFunction(
            ObjectInstance constructor,
            JsValue newTarget,
            FunctionKind kind,
            JsValue[] args)
        {
            // TODO var callerContext = _engine.GetExecutionContext(1);
            var callerContext = _engine.ExecutionContext;
            var callerRealm = callerContext.Realm;
            var calleeRealm = _engine.ExecutionContext.Realm;

            _engine._host.EnsureCanCompileStrings(callerRealm, calleeRealm);

            if (newTarget.IsUndefined())
            {
                newTarget = constructor;
            }

            Func<Intrinsics, ObjectInstance>? fallbackProto = null;
            switch (kind)
            {
                case FunctionKind.Normal:
                    fallbackProto = static intrinsics => intrinsics.Function.PrototypeObject;
                    break;
                case FunctionKind.Generator:
                case FunctionKind.AsyncGenerator:
                case FunctionKind.Async:
                default:
                    ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(kind), kind.ToString());
                    break;
            }

            var argCount = args.Length;
            var p = "";
            var body = "";

            if (argCount == 1)
            {
                body = TypeConverter.ToString(args[0]);
            }
            else if (argCount > 1)
            {
                var firstArg = args[0];
                p = TypeConverter.ToString(firstArg);
                for (var k = 1; k < argCount - 1; k++)
                {
                    var nextArg = args[k];
                    p += "," + TypeConverter.ToString(nextArg);
                }

                body = TypeConverter.ToString(args[argCount - 1]);
            }

            IFunction? function = null;
            try
            {
                string? functionExpression = null;
                if (argCount == 0)
                {
                    switch (kind)
                    {
                        case FunctionKind.Normal:
                            functionExpression = "function f(){}";
                            break;
                        case FunctionKind.Generator:
                            functionExpression = "function* f(){}";
                            break;
                        case FunctionKind.Async:
                            ExceptionHelper.ThrowNotImplementedException("Async functions not implemented");
                            break;
                        case FunctionKind.AsyncGenerator:
                            ExceptionHelper.ThrowNotImplementedException("Async generators not implemented");
                            break;
                        default:
                            ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(kind), kind.ToString());
                            break;
                    }
                }
                else
                {
                    switch (kind)
                    {
                        case FunctionKind.Normal:
                            functionExpression = "function f(";
                            break;
                        case FunctionKind.Generator:
                            functionExpression = "function* f(";
                            break;
                        case FunctionKind.Async:
                            ExceptionHelper.ThrowNotImplementedException("Async functions not implemented");
                            break;
                        case FunctionKind.AsyncGenerator:
                            ExceptionHelper.ThrowNotImplementedException("Async generators not implemented");
                            break;
                        default:
                            ExceptionHelper.ThrowArgumentOutOfRangeException(nameof(kind), kind.ToString());
                            break;
                    }

                    if (p.IndexOf('/') != -1)
                    {
                        // ensure comments don't screw up things
                        functionExpression += "\n" + p + "\n";
                    }
                    else
                    {
                        functionExpression += p;
                    }

                    functionExpression += ")";

                    if (body.IndexOf('/') != -1)
                    {
                        // ensure comments don't screw up things
                        functionExpression += "{\n" + body + "\n}";
                    }
                    else
                    {
                        functionExpression += "{" + body + "}";
                    }
                }

                var parser = new JavaScriptParser(functionExpression, ParserOptions);
                function = (IFunction) parser.ParseScript().Body[0];
            }
            catch (ParserException ex)
            {
                ExceptionHelper.ThrowSyntaxError(_engine.ExecutionContext.Realm, ex.Message);
            }

            var proto = GetPrototypeFromConstructor(newTarget, fallbackProto);
            var realmF = _realm;
            var scope = realmF.GlobalEnv;
            PrivateEnvironmentRecord? privateScope = null;

            var definition = new JintFunctionDefinition(_engine, function);
            FunctionInstance F = OrdinaryFunctionCreate(proto, definition, function.Strict ? FunctionThisMode.Strict : FunctionThisMode.Global, scope, privateScope);
            F.SetFunctionName(_functionNameAnonymous, force: true);

            if (kind == FunctionKind.Generator)
            {
                ExceptionHelper.ThrowNotImplementedException("generators not implemented");
            }
            else if (kind == FunctionKind.AsyncGenerator)
            {
                // TODO
                // Let prototype be ! OrdinaryObjectCreate(%AsyncGeneratorFunction.prototype.prototype%).
                // Perform DefinePropertyOrThrow(F, "prototype", PropertyDescriptor { [[Value]]: prototype, [[Writable]]: true, [[Enumerable]]: false, [[Configurable]]: false }).
                ExceptionHelper.ThrowNotImplementedException("async generators not implemented");
            }
            else if (kind == FunctionKind.Normal)
            {
                F.MakeConstructor();
            }

            return F;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-ordinaryfunctioncreate
        /// </summary>
        internal ScriptFunctionInstance OrdinaryFunctionCreate(
            ObjectInstance functionPrototype,
            JintFunctionDefinition function,
            FunctionThisMode thisMode,
            EnvironmentRecord scope,
            PrivateEnvironmentRecord? privateScope)
        {
            return new ScriptFunctionInstance(
                _engine,
                function,
                scope,
                thisMode,
                functionPrototype)
            {
                _privateEnvironment = privateScope,
                _realm = _realm
            };
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiatefunctionobject
        /// </summary>
        internal FunctionInstance InstantiateFunctionObject(
            JintFunctionDefinition functionDeclaration,
            EnvironmentRecord scope,
            PrivateEnvironmentRecord? privateScope)
        {
            return !functionDeclaration.Function.Generator
                ? InstantiateOrdinaryFunctionObject(functionDeclaration, scope, privateScope)
                : InstantiateGeneratorFunctionObject(functionDeclaration, scope, privateScope);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiateordinaryfunctionobject
        /// </summary>
        private FunctionInstance InstantiateOrdinaryFunctionObject(
            JintFunctionDefinition functionDeclaration,
            EnvironmentRecord scope,
            PrivateEnvironmentRecord? privateScope)
        {
            var F = OrdinaryFunctionCreate(
                _realm.Intrinsics.Function.PrototypeObject,
                functionDeclaration,
                functionDeclaration.ThisMode,
                scope,
                privateScope);

            var name = functionDeclaration.Name ?? "default";
            F.SetFunctionName(name);
            F.MakeConstructor();
            return F;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiategeneratorfunctionobject
        /// </summary>
        private FunctionInstance InstantiateGeneratorFunctionObject(
            JintFunctionDefinition functionDeclaration,
            EnvironmentRecord scope,
            PrivateEnvironmentRecord? privateScope)
        {
            // TODO generators
            return InstantiateOrdinaryFunctionObject(functionDeclaration, scope, privateScope);
        }
    }
}
