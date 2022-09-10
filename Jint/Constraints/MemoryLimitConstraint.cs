using Jint.Runtime;

namespace Jint.Constraints;

public sealed class MemoryLimitConstraint : Constraint
{
    private static readonly Func<long>? GetAllocatedBytesForCurrentThread;
    private readonly long _memoryLimit;
    private long _initialMemoryUsage;

    static MemoryLimitConstraint()
    {
        var methodInfo = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread");

        if (methodInfo != null)
        {
            GetAllocatedBytesForCurrentThread = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), null, methodInfo);
        }
    }

    internal MemoryLimitConstraint(long memoryLimit)
    {
        _memoryLimit = memoryLimit;
    }

    public override void Check()
    {
        if (_memoryLimit > 0)
        {
            if (GetAllocatedBytesForCurrentThread != null)
            {
                var memoryUsage = GetAllocatedBytesForCurrentThread() - _initialMemoryUsage;
                if (memoryUsage > _memoryLimit)
                {
                    ExceptionHelper.ThrowMemoryLimitExceededException($"Script has allocated {memoryUsage} but is limited to {_memoryLimit}");
                }
            }
            else
            {
                ExceptionHelper.ThrowPlatformNotSupportedException("The current platform doesn't support MemoryLimit.");
            }
        }
    }

    public override void Reset()
    {
        if (GetAllocatedBytesForCurrentThread != null)
        {
            _initialMemoryUsage = GetAllocatedBytesForCurrentThread();
        }
    }
}
