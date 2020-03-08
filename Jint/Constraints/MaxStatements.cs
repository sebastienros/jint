using Jint.Runtime;

namespace Jint.Constraints
{
    internal sealed class MaxStatements : IConstraint
    {
        private readonly int _maxStatements;
        private int _statementsCount;

        public MaxStatements(int maxStatements)
        {
            _maxStatements = maxStatements;
        }

        public void Check()
        {
            if (_maxStatements > 0 && _statementsCount++ > _maxStatements)
            {
                ExceptionHelper.ThrowStatementsCountOverflowException();
            }
        }

        public void Reset()
        {
            _statementsCount = 0;
        }
    }
}
