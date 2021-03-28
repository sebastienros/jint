using Jint.Native;
using Jint.Runtime.Environments;
using System.Collections;
using System.Collections.Generic;

namespace Jint.Runtime.Debugger
{
    public sealed class DebugScopes : IReadOnlyList<DebugScope>
    {
        private readonly HashSet<string> _foundBindings = new HashSet<string>();
        private readonly List<DebugScope> _scopes = new List<DebugScope>();

        internal DebugScopes(LexicalEnvironment environment)
        {
            Populate(environment);
        }

        /// <summary>
        /// Shortcut to Global scope
        /// </summary>
        public DebugScope Global { get; private set; }

        /// <summary>
        /// Shortcut to Local scope. Note that this is only present inside functions, and only includes
        /// function scope variables and block scope let/const that are declared at the top level of the
        /// function.
        /// </summary>
        public DebugScope Local { get; private set; }

        public DebugScope this[int index] => _scopes[index];
        public int Count => _scopes.Count;

        private void Populate(LexicalEnvironment environment)
        {
            bool inLocalScope = true;
            while (environment != null)
            {
                switch (environment._record)
                {
                    case GlobalEnvironmentRecord:
                        AddScope(DebugScopeType.Global, environment);
                        break;
                    case FunctionEnvironmentRecord:
                        AddScope(inLocalScope ? DebugScopeType.Local : DebugScopeType.Closure, environment);
                        // We're now in closure territory
                        inLocalScope = false;
                        break;
                    case ObjectEnvironmentRecord:
                        // If an ObjectEnvironmentRecord is not a GlobalEnvironmentRecord, it's With
                        AddScope(DebugScopeType.With, environment);
                        break;
                    case DeclarativeEnvironmentRecord der:
                        if (der._catchEnvironment)
                        {
                            AddScope(DebugScopeType.Catch, environment);
                        }
                        else if (environment._outer?._record is FunctionEnvironmentRecord)
                        {
                            // Like Chromium, we collapse a Function scope and function-top-level Block scope
                            // into a combined Local scope:
                            AddScope(inLocalScope ? DebugScopeType.Local : DebugScopeType.Closure, environment, environment._outer);
                            // We already handled the outer (function) scope, so skip it:
                            environment = environment._outer;
                            // We're now in closure territory
                            inLocalScope = false;
                        }
                        else
                        {
                            AddScope(DebugScopeType.Block, environment);
                        }
                        break;
                }

                environment = environment._outer;
            }
        }

        private void AddScope(DebugScopeType type, params LexicalEnvironment[] environments)
        {
            var bindings = new Dictionary<string, JsValue>();
            foreach (var env in environments)
            {
                PopulateBindings(bindings, env);
            }

            if (bindings.Count > 0)
            {
                var scope = new DebugScope(type, bindings);
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
                if (type == DebugScopeType.Global)
                {
                    Global = scope;
                }
            }
        }

        private void PopulateBindings(Dictionary<string, JsValue> bindings, LexicalEnvironment lex)
        {
            var bindingNames = lex._record.GetAllBindingNames();

            if (!_foundBindings.Contains("this") && lex._record.HasThisBinding())
            {
                bindings.Add("this", lex._record.GetThisBinding());
                _foundBindings.Add("this");
            }

            foreach (var name in bindingNames)
            {
                if (_foundBindings.Contains(name))
                {
                    // This binding is shadowed by earlier scope
                    continue;
                }
                var jsValue = lex._record.GetBindingValue(name, false);

                switch (jsValue)
                {
                    case null:
                        // Uninitialized consts and lets in scope are shown as "undefined" in recent Chromium debugger.
                        // TODO: Check if null result from GetBindingValue is only true for uninitialized const/let.
                        break;
                    default:
                        _foundBindings.Add(name);
                        bindings.Add(name, jsValue);
                        break;
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
