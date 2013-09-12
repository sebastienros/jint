using System;
using System.Linq;
using Jint.Native.Argument;
using Jint.Native.Object;
using Jint.Parser;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class ScriptFunctionInstance : FunctionInstance, IConstructor
    {
        private readonly Engine Engine;
        private readonly IFunctionDeclaration _functionDeclaration;

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="functionDeclaration"></param>
        /// <param name="scope"></param>
        /// <param name="strict"></param>
        public ScriptFunctionInstance(Engine engine, IFunctionDeclaration functionDeclaration, LexicalEnvironment scope, bool strict)
            : base(engine, functionDeclaration.Parameters.Select(x => x.Name).ToArray(), scope, strict)
        {
            _functionDeclaration = functionDeclaration;

            Engine = engine;
            Extensible = true;
            Prototype = engine.Function.PrototypeObject;

            DefineOwnProperty("length", new DataDescriptor(FormalParameters.Length) { Writable = false, Enumerable = false, Configurable = false }, false);

            var proto = engine.Object.Construct(Arguments.Empty);
            proto.DefineOwnProperty("constructor", new DataDescriptor(this) { Writable = true, Enumerable = false, Configurable = true }, false);
            DefineOwnProperty("prototype", new DataDescriptor(proto) { Writable = true, Enumerable = false, Configurable = true }, false);

            DefineOwnProperty("name", new DataDescriptor(_functionDeclaration.Id), false);
            
            if (strict)
            {
                var thrower = engine.Function.ThrowTypeError;
                DefineOwnProperty("caller", new AccessorDescriptor(thrower) { Enumerable = false, Configurable = false }, false);
                DefineOwnProperty("arguments", new AccessorDescriptor(thrower) { Enumerable = false, Configurable = false }, false);
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.1
        /// </summary>
        /// <param name="thisArg"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public override object Call(object thisArg, object[] arguments)
        {
            object thisBinding;

            // setup new execution context http://www.ecma-international.org/ecma-262/5.1/#sec-10.4.3
            if (this.Strict || Engine.Options.IsStrict())
            {
                thisBinding = thisArg;
            }
            else if (thisArg == Undefined.Instance || thisArg == Null.Instance)
            {
                thisBinding = Engine.Global;
            }
            else if (TypeConverter.GetType(thisArg) != Types.Object)
            {
                thisBinding = TypeConverter.ToObject(Engine, thisArg);
            }
            else
            {
                thisBinding = thisArg;
            }

            var localEnv = LexicalEnvironment.NewDeclarativeEnvironment(Engine, Scope);
            
            Engine.EnterExecutionContext(localEnv, localEnv, thisBinding);

            // Declaration Binding Instantiation http://www.ecma-international.org/ecma-262/5.1/#sec-10.5
            var env = localEnv.Record;
            var configurableBindings = false;

            //if (/* todo: if code is eval code */)
            //{
            //    configurableBindings = true;
            //}

            var argCount = arguments.Length;
            var n = 0;
            foreach (var argName in FormalParameters)
            {
                n++;
                var v = n > argCount ? Undefined.Instance : arguments[n-1];
                var argAlreadyDeclared = env.HasBinding(argName);
                if (!argAlreadyDeclared)
                {
                    env.CreateMutableBinding(argName);
                }

                env.SetMutableBinding(argName, v, Strict);
            }

            Engine.FunctionDeclarationBindings(_functionDeclaration, localEnv, true, Strict);

            var argumentsAlreadyDeclared = env.HasBinding("arguments");

            if (!argumentsAlreadyDeclared)
            {
                var argsObj = ArgumentsInstance.CreateArgumentsObject(Engine, this, FormalParameters, arguments, env, Strict);

                if (Strict)
                {
                    var declEnv = env as DeclarativeEnvironmentRecord;
                    
                    if (declEnv == null)
                    {
                        throw new ArgumentException();
                    }

                    declEnv.CreateImmutableBinding("arguments");
                    declEnv.InitializeImmutableBinding("arguments", argsObj);
                }
                else
                {
                    env.CreateMutableBinding("arguments");
                    env.SetMutableBinding("arguments", argsObj, false);
                }
            }

            // process all variable declarations in the current parser scope
            Engine.VariableDeclarationBinding(_functionDeclaration.VariableDeclarations, env, configurableBindings, Strict);

            var result = Engine.ExecuteStatement(_functionDeclaration.Body);
            
            Engine.LeaveExecutionContext();

            if (result.Type == Completion.Throw)
            {
                throw new JavaScriptException(result.Value);
            }

            return result.Value ?? Undefined.Instance;
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.2
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public ObjectInstance Construct(object[] arguments)
        {
            var proto = Get("prototype") as ObjectInstance;
            var obj = new ObjectInstance(Engine);
            obj.Extensible = true;
            obj.Prototype = proto ?? Engine.Object.PrototypeObject;

            var result = Call(obj, arguments) as ObjectInstance;
            if (result != null)
            {
                return result;
            }
            
            return obj;
        }

        public ObjectInstance PrototypeObject { get; private set; }
    }
}