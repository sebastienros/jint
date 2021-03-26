using Jint.Native;
using Jint.Runtime.Environments;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Jint.Runtime.Debugger
{
    public class DebugScopes : IReadOnlyList<DebugScope>
    {
        private readonly HashSet<string> foundBindings = new HashSet<string>();
        private readonly List<DebugScope> scopes = new List<DebugScope>();

        public DebugScopes(ExecutionContext context)
        {
            Populate(context);
        }

        private void Populate(ExecutionContext context)
        {
            bool inLocalScope = true;
            var env = context.LexicalEnvironment;
            while (env != null)
            {
                // Chromium devtools (v89) lists the following scopes (in scope chain order):
                // * Multiple Block scopes (limited to block scopes in innermost Local scope)
                // * Single Local scope (innermost, i.e. when in a nested function, outer function's Local scope will *not* be listed)
                // * Multiple Closure scopes (named by function name, closure's Block and Local scopes combined)
                // * Multiple Catch scopes
                // * Multiple With scopes (interestingly any inner Local scope will list normally Block scoped const/let as Local)
                // * Script scope (= top level block scope - let/const at top level)
                // * Global scope
                switch (env._record)
                {
                    case GlobalEnvironmentRecord:
                        AddScope(DebugScopeType.Global, env);
                        break;
                    case FunctionEnvironmentRecord:
                        AddScope(inLocalScope ? DebugScopeType.Local : DebugScopeType.Closure, env);
                        // We're now in closure territory
                        inLocalScope = false;
                        break;
                    case ObjectEnvironmentRecord:
                        // If an ObjectEnvironmentRecord is not a GlobalEnvironmentRecord, it's With
                        AddScope(DebugScopeType.With, env);
                        break;
                    case DeclarativeEnvironmentRecord der:
                        if (der._catchEnvironment)
                        {
                            AddScope(DebugScopeType.Catch, env);
                        }
                        else if (env._outer?._record is FunctionEnvironmentRecord)
                        {
                            // Like Chromium, we collapse a Function scope and function-top-level Block scope
                            // into a combined Local scope:
                            AddScope(inLocalScope ? DebugScopeType.Local : DebugScopeType.Closure, env, env._outer);
                            // We already handled the outer (function) scope, so skip it:
                            env = env._outer;
                            // We're now in closure territory
                            inLocalScope = false;
                        }
                        else
                        {
                            AddScope(DebugScopeType.Block, env);
                        }
                        break;
                }

                env = env._outer;
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
                scopes.Add(new DebugScope(type, bindings));
            }
        }

        private void PopulateBindings(Dictionary<string, JsValue> bindings, LexicalEnvironment lex)
        {
            var bindingNames = lex._record.GetAllBindingNames();

            if (!foundBindings.Contains("this") && lex._record.HasThisBinding())
            {
                bindings.Add("this", lex._record.GetThisBinding());
                foundBindings.Add("this");
            }

            foreach (var name in bindingNames)
            {
                if (foundBindings.Contains(name))
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
                        foundBindings.Add(name);
                        bindings.Add(name, jsValue);
                        break;
                }
            }
        }

        #region IReadOnlyList implementation

        public DebugScope this[int index] => scopes[index];
        public int Count => scopes.Count;

        public IEnumerator<DebugScope> GetEnumerator()
        {
            return scopes.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return scopes.GetEnumerator();
        }

        #endregion
    }
}
