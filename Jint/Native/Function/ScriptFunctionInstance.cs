using System.Collections.Generic;
using System.Linq;
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Parser.Ast;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Environments;

namespace Jint.Runtime
{
    /// <summary>
    /// 
    /// </summary>
    public class ScriptFunctionInstance : FunctionInstance
    {
        private readonly Engine _engine;
        private readonly Statement _body;
        private readonly IEnumerable<Identifier> _parameters;
        
        public ScriptFunctionInstance(Engine engine, Statement body, string name, Identifier[] parameters, ObjectInstance instancePrototype, ObjectInstance functionPrototype, LexicalEnvironment scope)
            : base(engine, instancePrototype, parameters, scope)
        {
            // http://www.ecma-international.org/ecma-262/5.1/#sec-13.2

            _engine = engine;
            _body = body;
            _parameters = parameters;
            Extensible = true;
            var len = parameters.Count();

            DefineOwnProperty("length", new DataDescriptor(len) { Writable = false, Enumerable = false, Configurable = false }, false);
            DefineOwnProperty("name", new DataDescriptor(name), false);
            instancePrototype.DefineOwnProperty("constructor", new DataDescriptor(this) { Writable = true, Enumerable = true, Configurable = true }, false);
            DefineOwnProperty("prototype", new DataDescriptor(functionPrototype) { Writable = true, Enumerable = true, Configurable = true }, false);
        }

        public override dynamic Call(object thisObject, dynamic[] arguments)
        {
            // todo: http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.1

            // setup new execution context http://www.ecma-international.org/ecma-262/5.1/#sec-10.4.3
            var localEnv = LexicalEnvironment.NewDeclarativeEnvironment(Scope);
            object thisArg;
            if (thisObject == Undefined.Instance || thisObject == Null.Instance)
            {
                thisArg = _engine.Global;
            }
            else
            {
                thisArg = thisObject;
            }

            _engine.EnterExecutionContext(localEnv, localEnv, thisArg);

            var env = localEnv.Record;

            int i = 0;
            foreach (var parameter in _parameters)
            {
                env.SetMutableBinding(parameter.Name, i < arguments.Length ? arguments[i++] : Undefined.Instance, false);
            }

            _engine.ExecuteStatement(_body);
            var result = _engine.CurrentExecutionContext.Return;

            _engine.LeaveExecutionContext();

            return result;
        }

        public virtual ObjectInstance Construct(dynamic[] arguments)
        {
            /// todo: http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.2

            var instance = new FunctionShim(_engine, this.Prototype, null, null);
            return instance;
        }
    }
}