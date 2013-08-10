using System.Collections.Generic;
using Jint.Native;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Represents a declarative environment record
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1.1
    /// </summary>
    public sealed class DeclarativeEnvironmentRecord : EnvironmentRecord
    {
        private readonly IDictionary<string, object> _bindings = new Dictionary<string, object>();

        public override bool HasBinding(string name)
        {
            return _bindings.ContainsKey(name);
        }

        public override void CreateMutableBinding(string name, bool canBeDeleted = true)
        {
            _bindings.Add(name, Undefined.Instance);
        }

        public override void SetMutableBinding(string name, object value, bool strict)
        {
            _bindings[name] = value;
        }

        public override object GetBindingValue(string name, bool strict)
        {
            return _bindings[name];
        }

        public override bool DeleteBinding(string name)
        {
            _bindings.Remove(name);
            return true;
        }

        public override object ImplicitThisValue()
        {
            return Undefined.Instance;
        }

        /// <summary>
        /// Creates a new but uninitialised immutable binding in an environment record.
        /// </summary>
        /// <param name="name">The identifier of the binding.</param>
        public void CreateImmutableBinding(string name)
        {
            CreateMutableBinding(name);
        }

        /// <summary>
        /// Sets the value of an already existing but uninitialised immutable binding in an environment record.
        /// </summary>
        /// <param name="name">The identifier of the binding.</param>
        /// <param name="value">The value of the binding.</param>
        public void InitializeImmutableBinding(string name, object value)
        {
            SetMutableBinding(name, value, false);
        }
    }
}
