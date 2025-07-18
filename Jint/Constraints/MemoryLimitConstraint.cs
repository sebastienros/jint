using Jint.Runtime;

namespace Jint.Constraints;

public sealed class MemoryLimitConstraint : Constraint
{
    private readonly long _memoryLimit;
    private long _initialMemoryUsage;

#if !NET8_0_OR_GREATER
    private static readonly Func<long>? _getAllocatedBytesForCurrentThread;

    static MemoryLimitConstraint()
    {
        var methodInfo = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread");

        if (methodInfo != null)
        {
            _getAllocatedBytesForCurrentThread = (Func<long>) Delegate.CreateDelegate(typeof(Func<long>), null, methodInfo);
        }
    }
#endif

    internal MemoryLimitConstraint(long memoryLimit)
    {
        _memoryLimit = memoryLimit;
    }

    public override void Check()
    {
        if (_memoryLimit <= 0)
        {
            return;
        }

#if NET8_0_OR_GREATER
        var usage = GC.GetAllocatedBytesForCurrentThread();
#else
        if (_getAllocatedBytesForCurrentThread == null)
        {
            Throw.PlatformNotSupportedException("The current platform doesn't support MemoryLimit.");
        }

        var usage = _getAllocatedBytesForCurrentThread();
#endif
        if (usage - _initialMemoryUsage > _memoryLimit)
        {
            Throw.MemoryLimitExceededException($"Script has allocated {usage - _initialMemoryUsage} but is limited to {_memoryLimit}");
        }
    }

    public override void Reset()
    {
#if NET8_0_OR_GREATER
        _initialMemoryUsage = GC.GetAllocatedBytesForCurrentThread();
#else
        _initialMemoryUsage = _getAllocatedBytesForCurrentThread?.Invoke() ?? 0;
#endif
    }
}
