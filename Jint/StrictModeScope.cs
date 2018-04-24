using System;

namespace Jint
{
    public readonly struct StrictModeScope : IDisposable
    {
        private readonly bool _strict;
        private readonly bool _force;
        private readonly int _forcedRefCount;

        [ThreadStatic] 
        private static int _refCount;

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
}
