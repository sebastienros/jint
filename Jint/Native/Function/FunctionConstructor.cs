using Esprima;
using Esprima.Ast;
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

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments, thisObject);
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-createdynamicfunction
        /// </summary>
        public ObjectInstance Construct(JsValue[] arguments, JsValue newTarget)
        {
            var argCount = arguments.Length;
            string p = "";
            string body = "";

            if (argCount == 1)
            {
                body = TypeConverter.ToString(arguments[0]);
            }
            else if (argCount > 1)
            {
                var firstArg = arguments[0];
                p = TypeConverter.ToString(firstArg);
                for (var k = 1; k < argCount - 1; k++)
                {
                    var nextArg = arguments[k];
                    p += "," + TypeConverter.ToString(nextArg);
                }

                body = TypeConverter.ToString(arguments[argCount-1]);
            }

            IFunction function = null;
            try
            {
                string functionExpression;
                if (argCount == 0)
                {
                    functionExpression = "function f(){}";
                }
                else
                {
                    functionExpression = "function f(";
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
            catch (ParserException)
            {
                ExceptionHelper.ThrowSyntaxError(_realm);
            }

            // TODO generators etc, rewrite logic
            var proto = GetPrototypeFromConstructor(newTarget, static intrinsics => intrinsics.Function.PrototypeObject);

            var functionObject = new ScriptFunctionInstance(
                Engine,
                function,
                _realm.GlobalEnv,
                function.Strict,
                proto)
            {
                _realm = _realm
            };

            functionObject.MakeConstructor();

            // the function is not actually a named function
            functionObject.SetFunctionName(_functionNameAnonymous, force: true);

            return functionObject;
        }

        /// <summary>
        /// https://tc39.es/ecma262/#sec-runtime-semantics-instantiatefunctionobject
        /// </summary>
        internal FunctionInstance InstantiateFunctionObject(JintFunctionDefinition functionDeclaration, EnvironmentRecord env)
        {
            var functionObject = new ScriptFunctionInstance(
                Engine,
                functionDeclaration,
                env,
                functionDeclaration.ThisMode)
            {
                _realm = _realm
            };

            functionObject.MakeConstructor();

            return functionObject;
        }
    }
}
