using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Esprima.Ast;
using Jint.Native;

namespace Jint.Runtime.Debugger
{
    public enum DebugScopeType
    {
        Global,
        Script,
        Local,
        Block,
        Catch,
        Closure,
        With,
        Eval
    }

    public class DebugScope : IReadOnlyDictionary<string, JsValue>
    {
        private IReadOnlyDictionary<string, JsValue> variables;

        public JsValue this[string key] => variables[key];

        public IEnumerable<string> Keys => variables.Keys;

        public IEnumerable<JsValue> Values => variables.Values;

        public DebugScopeType ScopeType { get; }
        public int Count => variables.Count;

        public DebugScope(DebugScopeType type, IReadOnlyDictionary<string, JsValue> variables)
        {
            ScopeType = type;
            this.variables = variables;
        }

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

    public class DebugScopes : IReadOnlyList<DebugScope>
    {
        private readonly List<DebugScope> scopes = new List<DebugScope>();

        public DebugScope this[int index] => scopes[index];
        // TODO: Right now we may have more than one of each type of scope
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

    public class DebugInformation : EventArgs
    {
        public DebugCallStack CallStack { get; set; }
        public Statement CurrentStatement { get; set; }
        public long CurrentMemoryUsage { get; set; }
        public DebugScopes Scopes { get; set; }
    }
}
