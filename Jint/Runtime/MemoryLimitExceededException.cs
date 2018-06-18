using System;

namespace Jint.Runtime
{
    public class MemoryLimitExceededException : Exception
    {
        public MemoryLimitExceededException() : base()
        {
        }

        public MemoryLimitExceededException(string message) : base(message)
        {
        }
    }
}
