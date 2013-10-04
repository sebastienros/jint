using System;

namespace Jint
{
    public class EvalCodeScope : IDisposable
    {
        private readonly bool _eval;
        private readonly bool _force;
        private readonly int _forcedRefCount;

        [ThreadStatic] 
        private static int _refCount;

        public EvalCodeScope(bool eval = true, bool force = false)
        {
            _eval = eval;
            _force = force;

            if (_force)
            {
                _forcedRefCount = _refCount;
                _refCount = 0;
            }

            if (_eval)
            {
                _refCount++;
            }

        }

        public void Dispose()
        {
            if (_eval)
            {
                _refCount--;
            }

            if (_force)
            {
                _refCount = _forcedRefCount;
            } 
        }

        public static bool IsEvalCode
        {
            get { return _refCount > 0; }
        }

        public static int RefCount
        {
            get
            {
                return _refCount;
            }
            set
            {
                _refCount = value;
            }
        }
    }
}
