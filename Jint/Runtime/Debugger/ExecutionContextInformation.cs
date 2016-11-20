using System;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Debugger
{
    public class ExecutionContextInformation : EventArgs
    {
        public ExecutionContext ExecutionContext { get; }

        public ExecutionContextInformation(ExecutionContext executionContext)
        {
            ExecutionContext = executionContext;
        }
    }
}