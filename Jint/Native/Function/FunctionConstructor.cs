using System;
using System.Linq;
using Esprima;
using Esprima.Ast;
using Jint.Native.Object;
using Jint.Native.String;
using Jint.Runtime;
using Jint.Runtime.Descriptors.Specialized;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public sealed class FunctionConstructor : FunctionInstance, IConstructor
    {
        private static readonly ParserOptions ParserOptions = new ParserOptions { AdaptRegexp = true, Tolerant = false };

        private FunctionConstructor(Engine engine):base(engine, null, null, false)
        {
        }

        public static FunctionConstructor CreateFunctionConstructor(Engine engine)
        {
            var obj = new FunctionConstructor(engine);
            obj.Extensible = true;

            // The initial value of Function.prototype is the standard built-in Function prototype object
            obj.PrototypeObject = FunctionPrototype.CreatePrototypeObject(engine);

            // The value of the [[Prototype]] internal property of the Function constructor is the standard built-in Function prototype object
            obj.Prototype = obj.PrototypeObject;

            obj.SetOwnProperty("prototype", new AllForbiddenPropertyDescriptor(obj.PrototypeObject));
            obj.SetOwnProperty("length", new AllForbiddenPropertyDescriptor(1));

            return obj;
        }

        public void Configure()
        {

        }

        public FunctionPrototype PrototypeObject { get; private set; }

        public override JsValue Call(JsValue thisObject, JsValue[] arguments)
        {
            return Construct(arguments);
        }

        private string[] ParseArgumentNames(string parameterDeclaration)
        {
            if (string.IsNullOrWhiteSpace(parameterDeclaration))
            {
                return System.Array.Empty<string>();
            }

            string[] values = parameterDeclaration.Split(ArgumentNameSeparator, StringSplitOptions.RemoveEmptyEntries);

            var newValues = new string[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                newValues[i] = StringPrototype.TrimEx(values[i]);
            }
            return newValues;
        }

        public ObjectInstance Construct(JsValue[] arguments)
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

            var parameters = this.ParseArgumentNames(p);
            IFunction function;
            try
            {
                var functionExpression = "function f(" + p + ") { " + body + "}";
                var parser = new JavaScriptParser(functionExpression, ParserOptions);
                function = parser.ParseProgram().Body.First().As<IFunction>();
            }
            catch (ParserException)
            {
                throw new JavaScriptException(Engine.SyntaxError);
            }

            var functionObject = new ScriptFunctionInstance(
                Engine,
                function,
                LexicalEnvironment.NewDeclarativeEnvironment(Engine, Engine.ExecutionContext.LexicalEnvironment),
                function.Strict
                ) { Extensible = true };

            return functionObject;

        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2
        /// </summary>
        /// <param name="functionDeclaration"></param>
        /// <returns></returns>
        public FunctionInstance CreateFunctionObject(FunctionDeclaration functionDeclaration)
        {
            var functionObject = new ScriptFunctionInstance(
                Engine,
                functionDeclaration,
                LexicalEnvironment.NewDeclarativeEnvironment(Engine, Engine.ExecutionContext.LexicalEnvironment),
                functionDeclaration.Strict
                ) { Extensible = true };

            return functionObject;
        }

        private FunctionInstance _throwTypeError;
        private static readonly char[] ArgumentNameSeparator = new[] { ',' };

        public FunctionInstance ThrowTypeError
        {
            get
            {
                if (_throwTypeError != null)
                {
                    return _throwTypeError;
                }

                _throwTypeError = new ThrowTypeError(Engine);
                return _throwTypeError;
            }
        }

        public object Apply(JsValue thisObject, JsValue[] arguments)
        {
            if (arguments.Length != 2)
            {
                throw new ArgumentException("Apply has to be called with two arguments.");
            }

            var func = thisObject.TryCast<ICallable>();
            var thisArg = arguments[0];
            var argArray = arguments[1];

            if (func == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            if (ReferenceEquals(argArray, Undefined) || ReferenceEquals(argArray, Undefined))
            {
                return func.Call(thisArg, Arguments.Empty);
            }

            var argArrayObj = argArray.TryCast<ObjectInstance>();
            if (argArrayObj == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var len = argArrayObj.Get("length");
            var n = TypeConverter.ToUint32(len);
            var argList = new JsValue[n];
            for (var index = 0; index < n; index++)
            {
                var indexName = TypeConverter.ToString(index);
                var nextArg = argArrayObj.Get(indexName);
                argList[index] = nextArg;
            }
            return func.Call(thisArg, argList);
        }
    }
}