using System;
using System.Threading;
using System.Threading.Tasks;

namespace Jint
{
    public readonly struct StrictModeScope
    {
        private readonly bool _strict;
        private readonly bool _force;
        private readonly int _refCount;

        private static readonly AsyncLocal<StrictModeScope> _currentScope = new AsyncLocal<StrictModeScope>();

        public static Task<T> WithStrictModeScope<T>(Func<T> func, bool strict = true, bool force = false)
        {
            return WithStrictModeScope(
                () => Task.FromResult(func()),
                strict,
                force);
        }

        public static async Task<T> WithStrictModeScope<T>(Func<Task<T>> func, bool strict = true, bool force = false)
        {
            _currentScope.Value = new StrictModeScope(strict, force);
            return await func().ConfigureAwait(false);
        }

        private StrictModeScope(bool strict = true, bool force = false)
        {
            _strict = strict;
            _force = force;

            if (_force)
            {
                _refCount = 0;
            }
            else
            {
                _refCount = _currentScope?.Value._refCount ?? 0;
            }

            if (_strict)
            {
                _refCount++;
            }
        }

        public static bool IsStrictModeCode => (_currentScope?.Value._refCount ?? 0) > 0;
    }
}
