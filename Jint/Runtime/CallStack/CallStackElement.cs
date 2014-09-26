namespace Jint.Runtime
{
    using Jint.Native;
    using Jint.Parser.Ast;

    public class CallStackElement
    {
        private string _shortDescription;

        public CallStackElement(CallExpression callExpression, JsValue function, string shortDescription)
        {
            _shortDescription = shortDescription;
            CallExpression = callExpression;
            Function = function;
        }

        public CallExpression CallExpression { get; private set; }

        public JsValue Function { get; private set; }

        public override string ToString()
        {
            return _shortDescription;
        }
    }
}
