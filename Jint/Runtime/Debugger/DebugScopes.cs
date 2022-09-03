using Jint.Runtime.Environments;
using System.Collections;

namespace Jint.Runtime.Debugger
{
    public sealed class DebugScopes : IReadOnlyList<DebugScope>
    {
        private readonly HashSet<string> _foundBindings = new();
        private readonly List<DebugScope> _scopes = new();

        internal DebugScopes(EnvironmentRecord environment)
        {
            Populate(environment);
        }

        /// <summary>
        /// Shortcut to Global scope.
        /// </summary>
        /// <remarks>
        /// Note that this only includes the object environment record of the Global scope - i.e. it doesn't
        /// include block scope bindings (let/const).
        /// </remarks>
        public DebugScope? Global { get; private set; }

        /// <summary>
        /// Shortcut to Local scope.
        /// </summary>
        /// <remarks>
        /// Note that this is only present inside functions, and doesn't include block scope bindings (let/const)
        /// </remarks>
        public DebugScope? Local { get; private set; }

        public DebugScope this[int index] => _scopes[index];
        public int Count => _scopes.Count;

        private void Populate(EnvironmentRecord? environment)
        {
            bool inLocalScope = true;
            while (environment != null)
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
            var bindings = new List<string>();
            PopulateBindings(bindings, record);

            if (bindings.Count > 0)
            {
                var scope = new DebugScope(type, record, bindings, isTopLevel);
                _scopes.Add(scope);
                switch (type)
                {
                    case DebugScopeType.Global:
                        Global = scope;
                        break;
                    case DebugScopeType.Local:
                        Local = scope;
                        break;
                }
            }
        }

        private void PopulateBindings(List<string> bindings, EnvironmentRecord record)
        {
            var bindingNames = record.GetAllBindingNames();

            foreach (var name in bindingNames)
            {
                // Only add non-shadowed bindings
                if (!_foundBindings.Contains(name))
                {
                    bindings.Add(name);
                    _foundBindings.Add(name);
                }
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
