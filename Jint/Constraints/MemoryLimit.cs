using Jint.Runtime;
using System;

namespace Jint.Constraints
{
    internal sealed class MemoryLimit : IConstraint
    {
        private static readonly Func<long> GetAllocatedBytesForCurrentThread;
        private readonly long _memoryLimit;
        private long _initialMemoryUsage;

        static MemoryLimit()
        {
            var methodInfo = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread");

            if (methodInfo != null)
            {
                GetAllocatedBytesForCurrentThread = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), null, methodInfo);
            }
        }

        public MemoryLimit(long memoryLimit)
        {
            if (GetAllocatedBytesForCurrentThread is null)
            {
                ExceptionHelper.ThrowPlatformNotSupportedException("The current platform doesn't support MemoryLimit.");
            }
            if (memoryLimit <= 0)
            {
                ExceptionHelper.ThrowArgumentException("Memory limit must be positive, non-zero value");
            }
            _memoryLimit = memoryLimit;
        }

        public void Check()
        {
            var memoryUsage = GetAllocatedBytesForCurrentThread() - _initialMemoryUsage;
            if (memoryUsage > _memoryLimit)
            {
                ThrowMemoryLimitExceededException(memoryUsage);
            }
        }

        private void ThrowMemoryLimitExceededException(long memoryUsage)
        {
            throw new MemoryLimitExceededException($"Script has allocated {memoryUsage} but is limited to {_memoryLimit}");
        }
        
        public void Reset()
        {
            if (GetAllocatedBytesForCurrentThread != null)
            {
                _initialMemoryUsage = GetAllocatedBytesForCurrentThread();
            }
        }
    }
}
