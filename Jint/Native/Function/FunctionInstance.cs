using Jint.Native.Object;
using Jint.Parser.Ast;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public abstract class FunctionInstance : ObjectInstance
    {
        private readonly Engine _engine;

        protected FunctionInstance(Engine engine, ObjectInstance prototype, Identifier[] parameters, LexicalEnvironment scope) : base(prototype)
        {
            _engine = engine;
            Parameters = parameters;
            Scope = scope;
        }

        /// <summary>
        /// Executed when a function object is used as a function
        /// </summary>
        /// <param name="interpreter"></param>
        /// <param name="state"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public abstract object Call(object thisObject, object[] arguments);

        public LexicalEnvironment Scope { get; private set; }
        public Identifier[] Parameters { get; private set; }

        public bool HasInstance(object instance)
        {
            var v = instance as ObjectInstance;
            if (v == null)
            {
                return false;
            }

            while (true)
            {
                v = v.Prototype;

                if (v == null)
                {
                    return false;
                }
                if (v == this.Prototype)
                {
                    return true;
                }
            }

            return false;
        }

        public override string Class
        {
            get
            {
                return "Function";
            }
        }
    }
}
