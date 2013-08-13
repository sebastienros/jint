using System.Collections.Generic;
using Jint.Native.Object;
using Jint.Parser.Ast;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Function
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class FunctionConstructor : FunctionInstance, IConstructor
    {
        private readonly Engine _engine;
        private readonly IEnumerable<Identifier> _parameters;

        public FunctionConstructor(Engine engine)
            : base(engine, engine.RootFunction, null, null, false)
        {
            _engine = engine;
            // http://www.ecma-international.org/ecma-262/5.1/#sec-13.2

            Extensible = true;
        }

        public override object Call(object thisObject, object[] arguments)
        {
            return Construct(arguments);
        }

        public ObjectInstance Construct(object[] arguments)
        {
            var instance = new FunctionShim(_engine, Prototype, null, null);
            instance.DefineOwnProperty("constructor", new DataDescriptor(Prototype) { Writable = true, Enumerable = false, Configurable = false }, false);

            return instance;
        }
    }
}