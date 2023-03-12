using Jint.Runtime.Environments;
using System.Collections;

namespace Jint.Runtime.Debugger
{
    public sealed class DebugScopes : IReadOnlyList<DebugScope>
    {
        private readonly List<DebugScope> _scopes = new();

        internal DebugScopes(EnvironmentRecord environment)
        {
            Populate(environment);
        }

        public DebugScope this[int index] => _scopes[index];
        public int Count => _scopes.Count;

        private void Populate(EnvironmentRecord? environment)
        {
            bool inLocalScope = true;
            while (environment is not null)
            {
                var record = environment;
                switch (record)
                {
                    case GlobalEnvironmentRecord global:
                        // Similarly to Chromium, we split the Global environment into Global and Script scopes
                        AddScope(DebugScopeType.Script, global._declarativeRecord);
                        AddScope(DebugScopeType.Global, new ObjectEnvironmentRecord(environment._engine, global._global, false, false));
                        break;
                    case FunctionEnvironmentRecord:
                        AddScope(inLocalScope ? DebugScopeType.Local : DebugScopeType.Closure, record);
                        // We're now in closure territory
                        inLocalScope = false;
                        break;
                    case ObjectEnvironmentRecord:
                        // If an ObjectEnvironmentRecord is not a GlobalEnvironmentRecord, it's With
                        AddScope(DebugScopeType.With, record);
                        break;
                    case ModuleEnvironmentRecord:
                        AddScope(DebugScopeType.Module, record);
                        break;
                    case DeclarativeEnvironmentRecord der:
                        if (der._catchEnvironment)
                        {
                            AddScope(DebugScopeType.Catch, record);
                        }
                        else
                        {
                            bool isTopLevel = environment._outerEnv is FunctionEnvironmentRecord;
                            AddScope(DebugScopeType.Block, record, isTopLevel: isTopLevel);
                        }
                        break;
                }

                environment = environment._outerEnv;
            }
        }

        private void AddScope(DebugScopeType type, EnvironmentRecord record, bool isTopLevel = false)
        {
            if (record.HasBindings())
            {
                var scope = new DebugScope(type, record, isTopLevel);
                _scopes.Add(scope);
            }
        }

        public IEnumerator<DebugScope> GetEnumerator()
        {
            return _scopes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _scopes.GetEnumerator();
        }
    }
}
