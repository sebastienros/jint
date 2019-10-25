using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jint
{
    public class EvalCodeScope
    {
        private readonly bool _eval;
        private readonly bool _force;
        private readonly int _refCount;

        private static readonly AsyncLocal<EvalCodeScope> _currentScope = new AsyncLocal<EvalCodeScope>();

        public static Task<T> WithEvalCodeScope<T>(Func<T> func, bool eval = true, bool force = false)
        {
            return WithEvalCodeScope(
                () => Task.FromResult(func()),
                eval, 
                force);
        }

        public static async Task<T> WithEvalCodeScope<T>(Func<Task<T>> func, bool eval = true, bool force = false)
        {
            _currentScope.Value = new EvalCodeScope(eval, force);
            return await func().ConfigureAwait(false);
        }

        private EvalCodeScope(bool eval = true, bool force = false)
        {
            _eval = eval;
            _force = force;

            if (_force)
            {
                _refCount = 0;
            }
            else
            {
                _refCount = _currentScope?.Value?._refCount ?? 0;
            }

            if (_eval)
            {
                _refCount++;
            }

        }

        public static bool IsEvalCode => _currentScope?.Value?._refCount > 0;

        public static int RefCount => _currentScope?.Value?._refCount ?? 0;
    }
}
