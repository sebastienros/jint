using Jint.Native;
using Jint.Parser.Ast;

namespace Jint.Runtime.Environments
{
    public sealed class ExecutionContext
    {
        public ExecutionContext()
        {
            Return = Undefined.Instance;
        }

        public LexicalEnvironment LexicalEnvironment { get; set; }
        public LexicalEnvironment VariableEnvironment { get; set; }
        public object ThisBinding { get; set; }

        /// <summary>
        /// Set when a break statement has been processed 
        /// </summary>
        public BreakStatement Break { get; set; }

        /// <summary>
        /// Set when a continue statement has been processed
        /// </summary>
        public ContinueStatement Continue { get; set; }

        /// <summary>
        /// Set when a return statement has been processed
        /// </summary>
        public object Return { get; set; }

    }
}
