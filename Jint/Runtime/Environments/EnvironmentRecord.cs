using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Jint.Native;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Base implementation of an Environment Record
    /// https://tc39.es/ecma262/#sec-environment-records
    /// </summary>
    public abstract class EnvironmentRecord : JsValue
    {
        protected internal readonly Engine _engine;
        protected internal EnvironmentRecord? _outerEnv;

        protected EnvironmentRecord(Engine engine) : base(InternalTypes.ObjectEnvironmentRecord)
        {
            _engine = engine;
        }

        /// <summary>
        /// Determines if an environment record has a binding for an identifier.
        /// </summary>
        /// <param name="name">The identifier of the binding</param>
        /// <returns><c>true</c> if it does and <c>false</c> if it does not.</returns>
        public abstract bool HasBinding(string name);

        internal abstract bool HasBinding(BindingName name);

        internal abstract bool TryGetBinding(
            BindingName name,
            bool strict,
            out Binding binding,
            [NotNullWhen(true)] out JsValue? value);

        /// <summary>
        /// Creates a new mutable binding in an environment record.
        /// </summary>
        /// <param name="name">The identifier of the binding.</param>
        /// <param name="canBeDeleted"><c>true</c> if the binding may be subsequently deleted.</param>
        public abstract void CreateMutableBinding(string name, bool canBeDeleted = false);

        /// <summary>
        /// Creates a new but uninitialized immutable binding in an environment record.
        /// </summary>
        /// <param name="name">The identifier of the binding.</param>
        /// <param name="strict"><c>false</c> if the binding may used before it's been initialized.</param>
        public abstract void CreateImmutableBinding(string name, bool strict = true);

        /// <summary>
        /// Set the value of an already existing but uninitialized binding in an Environment Record.
        /// </summary>
        /// <param name="name">The text of the bound name</param>
        /// <param name="value">The value for the binding.</param>
        public abstract void InitializeBinding(string name, JsValue value);

        /// <summary>
        /// Sets the value of an already existing mutable binding in an environment record.
        /// </summary>
        /// <param name="name">The identifier of the binding</param>
        /// <param name="value">The value of the binding.</param>
        /// <param name="strict">The identify strict mode references.</param>
        public abstract void SetMutableBinding(string name, JsValue value, bool strict);

        internal abstract void SetMutableBinding(BindingName name, JsValue value, bool strict);

        /// <summary>
        /// Returns the value of an already existing binding from an environment record.
        /// </summary>
        /// <param name="name">The identifier of the binding</param>
        /// <param name="strict">The identify strict mode references.</param>
        /// <return>The value of an already existing binding from an environment record.</return>
        public abstract JsValue GetBindingValue(string name, bool strict);

        /// <summary>
        /// Delete a binding from an environment record. The String value N is the text of the bound name If a binding for N exists, remove the binding and return true. If the binding exists but cannot be removed return false. If the binding does not exist return true.
        /// </summary>
        /// <param name="name">The identifier of the binding</param>
        /// <returns><true>true</true> if the deletion is successfull.</returns>
        public abstract bool DeleteBinding(string name);

        public abstract bool HasThisBinding();

        public abstract bool HasSuperBinding();

        public abstract JsValue WithBaseObject();

        public abstract bool HasBindings();

        /// <summary>
        /// Returns an array of all the defined binding names
        /// </summary>
        /// <returns>The array of all defined bindings</returns>
        internal abstract string[] GetAllBindingNames();

        public override object ToObject()
        {
            ExceptionHelper.ThrowNotSupportedException();
            return null;
        }

        public override bool Equals(JsValue? other)
        {
            ExceptionHelper.ThrowNotSupportedException();
            return false;
        }

        public abstract JsValue GetThisBinding();

        public JsValue? NewTarget { get; protected set; }

        /// <summary>
        /// Helper to cache JsString/Key when environments use different lookups.
        /// </summary>
        [DebuggerDisplay("\"{Key.Name}\"")]
        internal sealed class BindingName
        {
            public readonly Key Key;
            public readonly JsString Value;
            public readonly bool HasEvalOrArguments;
            public readonly JsValue? CalculatedValue;

            public BindingName(string value)
            {
                var key = (Key) value;
                Key = key;
                Value = JsString.Create(value);
                HasEvalOrArguments = key == KnownKeys.Eval || key == KnownKeys.Arguments;
                if (key == KnownKeys.Undefined)
                {
                    CalculatedValue = Undefined;
                }
            }
        }
    }
}

