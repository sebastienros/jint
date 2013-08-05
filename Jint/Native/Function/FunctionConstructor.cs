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
    public class FunctionConstructor : FunctionInstance
    {
        private readonly IEnumerable<Identifier> _parameters;

        public FunctionConstructor(ObjectInstance prototype)
            : base(prototype, null, null)
        {
            // http://www.ecma-international.org/ecma-262/5.1/#sec-13.2

            Extensible = true;
            
        }

        public override dynamic Call(Engine engine, object thisObject, dynamic[] arguments)
        {
            return Construct(arguments);
        }

        public virtual ObjectInstance Construct(dynamic[] arguments)
        {
            var instance = new FunctionShim(Prototype, null, null);
            instance.DefineOwnProperty("constructor", new DataDescriptor(Prototype) { Writable = true, Enumerable = false, Configurable = false }, false);

            return instance;
        }
    }
}