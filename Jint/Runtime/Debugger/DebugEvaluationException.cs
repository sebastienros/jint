using System;

namespace Jint.Runtime.Debugger
{
    public class DebugEvaluationException : Exception
    {
        public DebugEvaluationException(string message) : base(message)
        {
        }

        public DebugEvaluationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
