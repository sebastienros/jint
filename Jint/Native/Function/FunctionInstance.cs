using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Environments;

namespace Jint.Native.Function
{
    public abstract class FunctionInstance : ObjectInstance, ICallable
    {
        private readonly Engine _engine;

        protected FunctionInstance(Engine engine, string[] parameters, LexicalEnvironment scope, bool strict) : base(engine)
        {
            _engine = engine;
            FormalParameters = parameters;
            Scope = scope;
            Strict = strict;
        }

        /// <summary>
        /// Executed when a function object is used as a function
        /// </summary>
        /// <param name="thisObject"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public abstract object Call(object thisObject, object[] arguments);

        public LexicalEnvironment Scope { get; private set; }
        
        public string[] FormalParameters { get; private set; }
        public bool Strict { get; private set; }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.3.5.3
        /// </summary>
        /// <param name="instance"></param>
        /// <returns></returns>
        public virtual bool HasInstance(object v)
        {
            var vObj = v as ObjectInstance;
            if (vObj == null)
            {
                return false;
            }

            var o = Get("prototype") as ObjectInstance;
            
            if (o == null)
            {
                throw new JavaScriptException(_engine.TypeError);    
            }

            while (true)
            {
                vObj = vObj.Prototype;

                if (vObj == null)
                {
                    return false;
                }
                if (vObj == o)
                {
                    return true;
                }
            }
        }

        public override string Class
        {
            get
            {
                return "Function";
            }
        }

        /// <summary>
        /// http://www.ecma-international.org/ecma-262/5.1/#sec-15.3.5.4
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public override object Get(string propertyName)
        {
            var v = base.Get(propertyName);

            var f = v as FunctionInstance;
            if (propertyName == "caller" && f != null && f.Strict)
            {
                throw new JavaScriptException(_engine.TypeError);
            }

            return v;
        }
    }
}
