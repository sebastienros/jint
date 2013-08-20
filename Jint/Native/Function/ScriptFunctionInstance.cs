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
        private readonly Engine _engine;
        private readonly IFunctionDeclaration _functionDeclaration;
        
        public ScriptFunctionInstance(Engine engine, IFunctionDeclaration functionDeclaration, ObjectInstance instancePrototype, ObjectInstance functionPrototype, LexicalEnvironment scope, bool strict)
            : base(engine, instancePrototype, functionDeclaration.Parameters.ToArray(), scope, strict)
        {
            // http://www.ecma-international.org/ecma-262/5.1/#sec-13.2

            _engine = engine;
            _functionDeclaration = functionDeclaration;
            Extensible = true;
            var len = functionDeclaration.Parameters.Count();

            DefineOwnProperty("length", new DataDescriptor(len) { Writable = false, Enumerable = false, Configurable = false }, false);
            DefineOwnProperty("name", new DataDescriptor(_functionDeclaration.Id), false);
            instancePrototype.DefineOwnProperty("constructor", new DataDescriptor(this) { Writable = true, Enumerable = true, Configurable = true }, false);
            DefineOwnProperty("prototype", new DataDescriptor(functionPrototype) { Writable = true, Enumerable = true, Configurable = true }, false);

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
            if (_engine.Options.IsStrict())
            {
                thisBinding = thisArg;
            }
            else if (thisArg == Undefined.Instance || thisArg == Null.Instance)
            {
                thisBinding = _engine.Global;
            }
            else if (TypeConverter.GetType(thisArg) != TypeCode.Object)
            {
                thisBinding = TypeConverter.ToObject(_engine, thisArg);
            }
            else
            {
                thisBinding = thisArg;
            }

            var localEnv = LexicalEnvironment.NewDeclarativeEnvironment(_engine, Scope);
            
            _engine.EnterExecutionContext(localEnv, localEnv, thisBinding);

            // Declaration Binding Instantiation http://www.ecma-international.org/ecma-262/5.1/#sec-10.5
            var env = localEnv.Record;
            var configurableBindings = false;

            var argCount = arguments.Length;
            var n = 0;
            foreach (var parameter in _functionDeclaration.Parameters)
            {
                var argName = parameter.Name;
                n++;
                var v = n > argCount ? Undefined.Instance : arguments[n-1];
                var argAlreadyDeclared = env.HasBinding(argName);
                if (!argAlreadyDeclared)
                {
                    env.CreateMutableBinding(argName);
                }

                env.SetMutableBinding(argName, v, Strict);
            }

            _engine.FunctionDeclarationBindings(_functionDeclaration, localEnv, true, Strict);

            var argumentsAlreadyDeclared = env.HasBinding("arguments");

            if (!argumentsAlreadyDeclared)
            {
                var argsObj = ArgumentsInstance.CreateArgumentsObject(_engine, this, _functionDeclaration.Parameters.Select(x => x.Name).ToArray(), arguments, env, Strict);

                if (Strict)
                {
                    var declEnv = env as DeclarativeEnvironmentRecord;
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
            _engine.VariableDeclarationBinding(_functionDeclaration, env, configurableBindings, Strict);

            var result = _engine.ExecuteStatement(_functionDeclaration.Body);
            
            _engine.LeaveExecutionContext();

            if (result.Type == Completion.Throw)
            {
                throw new JavaScriptException(result.Value);
            }

            return result;
        }

        public ObjectInstance Construct(object[] arguments)
        {
            // todo: http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.2

            var instance = new FunctionShim(_engine, Prototype, null, null);
            return instance;
        }
    }
}