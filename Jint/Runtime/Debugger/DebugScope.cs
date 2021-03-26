using System.Collections;
using System.Collections.Generic;
using Jint.Native;

namespace Jint.Runtime.Debugger
{
    public class DebugScope : IReadOnlyDictionary<string, JsValue>
    {
        private IReadOnlyDictionary<string, JsValue> variables;

        public DebugScopeType ScopeType { get; }

        public DebugScope(DebugScopeType type, IReadOnlyDictionary<string, JsValue> variables)
        {
            ScopeType = type;
            this.variables = variables;
        }

        #region IReadOnlyDictionary implementation

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

        #endregion
    }
}
