using System;
using System.Collections.Generic;
using Jint.Native.Object;
using Jint.Parser.Ast;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class FunctionConstructor : FunctionInstance, IConstructor
    {
        private FunctionConstructor(Engine engine):base(engine, null, null, false)
        {
        }

        public static FunctionConstructor CreateFunctionConstructor(Engine engine)
        {
            var obj = new FunctionConstructor(engine);


            // The initial value of Function.prototype is the standard built-in Function prototype object
            obj.PrototypeObject = FunctionPrototype.CreatePrototypeObject(engine);
            
            // The value of the [[Prototype]] internal property of the Function constructor is the standard built-in Function prototype object 
            obj.Prototype = obj.PrototypeObject;
            
            obj.FastAddProperty("prototype", obj.PrototypeObject, false, false, false);

            obj.FastAddProperty("length", 1, false, false, false);

            return obj;
        }

        public void Configure()
        {

        }

        public FunctionPrototype PrototypeObject { get; private set; }

        public override object Call(object thisObject, object[] arguments)
        {
            return Construct(arguments);
        }

        public ObjectInstance Construct(object[] arguments)
        {
            var instance = new FunctionShim(Engine, null, Engine.GlobalEnvironment);
            instance.Extensible = true;
            instance.DefineOwnProperty("constructor", new DataDescriptor(Prototype) { Writable = true, Enumerable = false, Configurable = false }, false);

            return instance;
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

        public object Apply(object thisObject, object[] arguments)
        {
            if (arguments.Length != 2)
            {
                throw new ArgumentException("Apply has to be called with two arguments.");
            }

            var func = thisObject as ICallable;
            var thisArg = arguments[0];
            var argArray = arguments[1];

            if (func == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            if (argArray == Null.Instance || argArray == Undefined.Instance)
            {
                return func.Call(thisArg, Arguments.Empty);
            }

            var argArrayObj = argArray as ObjectInstance;
            if (argArrayObj == null)
            {
                throw new JavaScriptException(Engine.TypeError);
            }

            var len = argArrayObj.Get("length");
            var n = TypeConverter.ToUint32(len);
            var argList = new List<object>();
            for (var index = 0; index < n; index++)
            {
                var indexName = index.ToString();
                var nextArg = argArrayObj.Get(indexName);
                argList.Add(nextArg);
            }
            return func.Call(thisArg, argList.ToArray());
        }
    }
}