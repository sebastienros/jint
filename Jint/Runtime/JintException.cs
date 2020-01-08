using System;
using System.Runtime.Serialization;

namespace Jint.Runtime
{
    /// <summary>
    /// Base class for exceptions thrown by Jint.
    /// </summary>
    [Serializable]
    public abstract class JintException : Exception
    {
        protected JintException()
        {
        }

        protected JintException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        protected JintException(string message) : base(message)
        {
        }

        protected JintException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}