using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Jint.Runtime.Debugger
{
    public class DebugScopes : IReadOnlyList<DebugScope>
    {
        private readonly List<DebugScope> scopes = new List<DebugScope>();

        public DebugScope this[int index] => scopes[index];
        // TODO: Currently, this is only used for tests. But we may have more than one of each type of scope, so find a different API for DebugScopes
        public DebugScope this[DebugScopeType type] => scopes.SingleOrDefault(s => s.ScopeType == type);

        public int Count => scopes.Count;

        public IEnumerator<DebugScope> GetEnumerator()
        {
            return scopes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return scopes.GetEnumerator();
        }

        internal void Add(DebugScope scope)
        {
            scopes.Add(scope);
        }
    }
}
