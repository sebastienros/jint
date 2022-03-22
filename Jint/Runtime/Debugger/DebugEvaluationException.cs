using System;

namespace Jint.Runtime.Debugger
{
    /// <summary>
    /// Thrown when an evaluation executed through the DebugHandler results in any type of error - parsing or runtime.
    /// </summary>
    public sealed class DebugEvaluationException : Exception
    {
        public DebugEvaluationException(string message) : base(message)
        {
        }

        public DebugEvaluationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
