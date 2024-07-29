using System.Runtime.InteropServices;

namespace Jint;

[StructLayout(LayoutKind.Auto)]
internal readonly struct StrictModeScope : IDisposable
{
    private readonly bool _strict;
    private readonly bool _force;
    private readonly ushort _forcedRefCount;

    [ThreadStatic]
    private static ushort _refCount;

    public StrictModeScope(bool strict = true, bool force = false)
    {
        _strict = strict;
        _force = force;

        if (_force)
        {
            _forcedRefCount = _refCount;
            _refCount = 0;
        }
        else
        {
            _forcedRefCount = 0;
        }

        if (_strict)
        {
            _refCount++;
        }
    }

    public void Dispose()
    {
        if (_strict)
        {
            _refCount--;
        }

        if (_force)
        {
            _refCount = _forcedRefCount;
        }
    }

    public static bool IsStrictModeCode => _refCount > 0;
}