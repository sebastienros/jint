using System;

namespace Jint
{
    public class StrictModeScope : IDisposable
    {
        private readonly bool _strict;

        [ThreadStatic] 
        private static int _refCount;

        public StrictModeScope(bool strict = true)
        {
            _strict = strict;
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
        }

        public static bool IsStrictModeCode
        {
            get { return _refCount > 0; }
        }
    }
}
