using System;
using System.Collections;
using System.Collections.Generic;
using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Debugger
{
    /// <summary>
    /// Scope information, bindings, and values for a single scope in the scope chain
    /// </summary>
    public sealed class DebugScope
    {
        private readonly EnvironmentRecord _record;
        private readonly List<string> _bindingNames;
        private readonly DebugBindings _bindings;

        internal DebugScope(DebugScopeType type, EnvironmentRecord record, List<string> bindingNames, bool isTopLevel)
        {
            ScopeType = type;
            _record = record;
            _bindingNames = bindingNames;
            IsTopLevel = isTopLevel;
            _bindings = new DebugBindings(_record, bindingNames);
        }

        /// <summary>
        /// The type of scope. Scope types are the same as defined by Chrome devtools protocol.
        /// </summary>
        public DebugScopeType ScopeType { get; }

        /// <summary>
        /// For <see cref="DebugScopeType.Block">block</see> scopes, indicates whether this scope is at the top level of a containing function.
        /// </summary>
        /// <remarks>
        /// Block scopes at the top level of a function are combined with Local scope in Chromium and devtools protocol.
        /// This property facilitates implementing the same "flattening" in e.g. a UI. Because empty scopes are excluded in the scope chain,
        /// top level cannot be determined from the scope chain order alone.
        /// </remarks>
        public bool IsTopLevel { get; }

        /// <summary>
        /// Number of non-shadowed bindings in the scope.
        /// </summary>
        public int BindingCount => _bindingNames.Count;

        /// <summary>
        /// Names of all non-shadowed bindings in the scope.
        /// </summary>
        public IReadOnlyList<string> BindingNames => _bindingNames;

        /// <summary>
        /// Name and value of all non-shadowed bindings in the scope.
        /// </summary>
        public IReadOnlyList<DebugBinding> Bindings => _bindings;

        /// <summary>
        /// Retrieves the value of a specific binding. Note that some bindings (e.g. uninitialized let) may return null.
        /// </summary>
        /// <param name="name">Binding name</param>
        /// <returns>Value of the binding</returns>
        public JsValue this[string name] => _record.GetBindingValue(name, strict: false);
    }
}
