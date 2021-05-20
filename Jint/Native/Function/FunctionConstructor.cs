using Esprima;
using Esprima.Ast;
using Jint.Native.Generator;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;
using Jint.Runtime.Interpreter;

namespace Jint.Native.Function
{
    public sealed class FunctionConstructor : FunctionInstance, IConstructor
    {
        private static readonly ParserOptions ParserOptions = new ParserOptions { AdaptRegexp = true, Tolerant = false };
        private static readonly JsString _functionName = new JsString("Function");
        private static readonly JsString _functionNameAnonymous = new JsString("anonymous");

        private FunctionConstructor(Engine engine)
            : base(engine, _functionName)
        {
        }

        public static FunctionConstructor CreateFunctionConstructor(Engine engine)
        {
            var obj = new FunctionConstructor(engine)
            {
                PrototypeObject = FunctionPrototype.CreatePrototypeObject(engine)
            };

            // The initial value of Function.prototype is the standard built-in Function prototype object

            // The value of the [[Prototype]] internal property of the Function constructor is the standard built-in Function prototype object
            obj._prototype = obj.PrototypeObject;

            obj._prototypeDescriptor = new PropertyDescriptor(obj.PrototypeObject, PropertyFlag.AllForbidden);
            obj._length = new PropertyDescriptor(JsNumber.One, PropertyFlag.Configurable);

            return obj;
        }

        public FunctionPrototype PrototypeObject { get; private set; }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments, thisObject);
        }

        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var function = CreateDynamicFunction(
                _engine,
                this,
                newTarget,
                FunctionKind.Normal,
                arguments);

            return function;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-createdynamicfunction
        /// </summary>
        internal static FunctionInstance CreateDynamicFunction(
            Engine engine,
            ObjectInstance constructor,
            JsValue newTarget,
            FunctionKind kind,
            JsValue[] args)
        {
            if (newTarget.IsUndefined())
            {
                newTarget = constructor;
            }

            ObjectInstance fallbackProto = null;
            switch (kind)
            {
                case FunctionKind.Normal:
                    fallbackProto = engine.Function.PrototypeObject;
                    break;
                case FunctionKind.Generator:
                    fallbackProto = engine.GeneratorFunction.PrototypeObject;
                    break;
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

            IFunction function = null;
            try
            {
                string functionExpression;
                if (argCount == 0)
                {
                    functionExpression = kind switch
                    {
                        FunctionKind.Normal => "function f(){}",
                        FunctionKind.Generator  => "function* f(){}",
                        _ => ExceptionHelper.ThrowArgumentOutOfRangeException<string>(nameof(kind), kind.ToString())
                    };
                }
                else
                {
                    functionExpression = kind switch
                    {
                        FunctionKind.Normal => "function f(",
                        FunctionKind.Generator  => "function* f(",
                        _ => ExceptionHelper.ThrowArgumentOutOfRangeException<string>(nameof(kind), kind.ToString())
                    };
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
                ExceptionHelper.ThrowSyntaxError(engine, ex.Message);
            }

            var proto = GetPrototypeFromConstructor(newTarget, fallbackProto);
            var thisMode = function.Strict
                ? FunctionThisMode.Strict
                : FunctionThisMode.Global;

            FunctionInstance F = new ScriptFunctionInstance(
                engine,
                new JintFunctionDefinition(engine, function),
                engine.GlobalEnvironment,
                thisMode,
                proto);

            if (kind == FunctionKind.Generator)
            {
                F = new GeneratorFunctionInstance(engine, F);
            }
            else if (kind == FunctionKind.AsyncGenerator)
            {
                // TODO
                // Let prototype be ! OrdinaryObjectCreate(%AsyncGeneratorFunction.prototype.prototype%).
                // Perform DefinePropertyOrThrow(F, "prototype", PropertyDescriptor { [[Value]]: prototype, [[Writable]]: true, [[Enumerable]]: false, [[Configurable]]: false }).
                ExceptionHelper.ThrowNotImplementedException();
            }
            else if (kind == FunctionKind.Normal)
            {
                F.MakeConstructor();
            }
            
            F.SetFunctionName(_functionNameAnonymous, force: true);
            return F;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiatefunctionobject
        /// </summary>
        internal FunctionInstance InstantiateFunctionObject(FunctionDeclaration functionDeclaration, EnvironmentRecord scope)
        {
            var strict = functionDeclaration.Strict || _engine._isStrict;
            FunctionInstance F = new ScriptFunctionInstance(
                Engine,
                functionDeclaration,
                scope,
                strict,
                _engine.Function.PrototypeObject);;
            
            var name = functionDeclaration.Id?.Name ?? "default";
            if (functionDeclaration.Generator)
            {
                // https://tc39.es/ecma262/#sec-generator-function-definitions-runtime-semantics-instantiatefunctionobject
                F = new GeneratorFunctionInstance(_engine, F);
            }
            else
            {
                // https://tc39.es/ecma262/#sec-function-definitions-runtime-semantics-instantiatefunctionobject
                F.MakeConstructor();
            }

            F.SetFunctionName(name);
            return F;
        }
    }
}