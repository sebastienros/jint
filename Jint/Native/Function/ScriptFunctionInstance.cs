using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class ScriptFunctionInstance : FunctionInstance, IConstructor
    {
        private readonly Engine _engine;
        private readonly Statement _body;
        private readonly IEnumerable<Identifier> _parameters;
        
        public ScriptFunctionInstance(Engine engine, Statement body, string name, Identifier[] parameters, ObjectInstance instancePrototype, ObjectInstance functionPrototype, LexicalEnvironment scope, bool strict)
            : base(engine, instancePrototype, parameters, scope, strict)
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

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2.1
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public override object Call(object thisObject, object[] arguments)
        {
            object thisBinding;

            // setup new execution context http://www.ecma-international.org/ecma-262/5.1/#sec-10.4.3
            if (_engine.Options.IsStrict())
            {
                thisBinding = thisObject;
            }
            else if (thisObject == Undefined.Instance || thisObject == Null.Instance)
            {
                thisBinding = _engine.Global;
            }
            else if (TypeConverter.GetType(thisObject) != TypeCode.Object)
            {
                thisBinding = TypeConverter.ToObject(_engine, thisObject);
            }
            else
            {
                thisBinding = thisObject;
            }

            var localEnv = LexicalEnvironment.NewDeclarativeEnvironment(Scope);
            
            _engine.EnterExecutionContext(localEnv, localEnv, thisBinding);

            var env = localEnv.Record;

            int i = 0;
            foreach (var parameter in _parameters)
            {
                env.SetMutableBinding(parameter.Name, i < arguments.Length ? arguments[i++] : Undefined.Instance, false);
            }

            var result = _engine.ExecuteStatement(_body);
            
            _engine.LeaveExecutionContext();

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