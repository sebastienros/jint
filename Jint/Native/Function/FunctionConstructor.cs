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

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-13.2
        /// </summary>
        /// <param name="functionDeclaration"></param>
        /// <returns></returns>
        public FunctionInstance CreateFunctionObject(FunctionDeclaration functionDeclaration)
        {
            var functionObject = new ScriptFunctionInstance(
                _engine,
                functionDeclaration,
                _engine.Function.Prototype /* instancePrototype */,
                _engine.Object.Construct(Arguments.Empty) /* functionPrototype */,
                LexicalEnvironment.NewDeclarativeEnvironment(_engine, _engine.ExecutionContext.LexicalEnvironment),
                functionDeclaration.Strict
                ) { Extensible = true };

            return functionObject;
        }
    }
}