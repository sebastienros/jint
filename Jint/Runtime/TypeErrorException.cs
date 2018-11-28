using System;

namespace Jint.Runtime
{
    /// <summary>
    /// Workaround for situation where engine is not easily accessible.
    /// </summary>
    internal sealed class TypeErrorException : Exception
    {
        public TypeErrorException(string message) : base(message)
        {
        }
    }
}