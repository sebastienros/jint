using Jint.Parser.Ast;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Debugger
{
    public class StackFrame
    {
        private readonly string value;
        public Statement CallStatement { get; }
        public CallExpression CallExpression { get; }
        public ExecutionContext ExecutionContext { get; }

        public StackFrame(string value, Statement callStatement, CallExpression callExpression, ExecutionContext executionContext)
        {
            this.value = value;
            CallStatement = callStatement;
            CallExpression = callExpression;
            ExecutionContext = executionContext;
        }

        public override string ToString()
        {
            return this.value;
        }

        public static implicit operator string(StackFrame frame)
        {
            return frame.ToString();
        }
    }
}