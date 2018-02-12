using System;

namespace Jint.Runtime
{
    public class MemoryLimitExceededException : Exception
    {
        public MemoryLimitExceededException() : base("The memory limit has been exceeded.")
        {
        }
    }
}
