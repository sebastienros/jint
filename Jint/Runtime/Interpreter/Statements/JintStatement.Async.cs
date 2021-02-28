using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Jint.Runtime.Interpreter.Statements
{
    internal abstract partial class JintStatement
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Task<Completion> ExecuteAsync()
        {
            _engine._lastSyntaxNode = _statement;
            _engine.RunBeforeExecuteStatementChecks(_statement);

            if (!_initialized)
            {
                Initialize();
                _initialized = true;
            }

            return ExecuteInternalAsync();
        }

        protected abstract Task<Completion> ExecuteInternalAsync();
    }
}