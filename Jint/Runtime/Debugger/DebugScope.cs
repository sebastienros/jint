using System.Collections;
using System.Collections.Generic;
using Jint.Native;

namespace Jint.Runtime.Debugger
{
    /// <summary>
    /// Dictionary of bindings for a single scope in the scope chain
    /// </summary>
    public sealed class DebugScope : IReadOnlyDictionary<string, JsValue>
    {
        private IReadOnlyDictionary<string, JsValue> variables;

        internal DebugScope(DebugScopeType type, IReadOnlyDictionary<string, JsValue> variables)
        {
            ScopeType = type;
            this.variables = variables;
        }

        /// <summary>
        /// The type of scope. Scope types are the same as defined by Chrome devtools protocol.
        /// </summary>
        public DebugScopeType ScopeType { get; }

        public JsValue this[string key] => variables[key];
        public IEnumerable<string> Keys => variables.Keys;
        public IEnumerable<JsValue> Values => variables.Values;
        public int Count => variables.Count;

        public bool ContainsKey(string key)
        {
            return variables.ContainsKey(key);
        }

        public IEnumerator<KeyValuePair<string, JsValue>> GetEnumerator()
        {
            return variables.GetEnumerator();
        }

        public bool TryGetValue(string key, out JsValue value)
        {
            return variables.TryGetValue(key, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return variables.GetEnumerator();
        }
    }
}
