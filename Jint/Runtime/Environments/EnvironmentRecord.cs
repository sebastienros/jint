using Jint.Native;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Base implementation of an Environment Record
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1
    /// </summary>
    public abstract class EnvironmentRecord : JsValue
    {
        protected readonly Engine _engine;

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

        internal abstract bool TryGetBinding(
            in BindingName name,
            bool strict,
            out Binding binding,
            out JsValue value);

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

        internal abstract void SetMutableBinding(in BindingName name, JsValue value, bool strict);

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

        /// <summary>
        /// Returns the value to use as the <c>this</c> value on calls to function objects that are obtained as binding values from this environment record.
        /// </summary>
        /// <returns>The value to use as <c>this</c>.</returns>
        public abstract JsValue ImplicitThisValue();

        /// <summary>
        /// Returns an array of all the defined binding names
        /// </summary>
        /// <returns>The array of all defined bindings</returns>
        internal abstract string[] GetAllBindingNames();

        public override object ToObject()
        {
            return ExceptionHelper.ThrowNotSupportedException<object>();
        }

        public override bool Equals(JsValue other)
        {
            return ExceptionHelper.ThrowNotSupportedException<bool>();
        }

        public abstract JsValue GetThisBinding();

        public JsValue NewTarget { get; protected set; }

        /// <summary>
        /// Helper to cache JsString/Key when environments use different lookups.
        /// </summary>
        internal readonly struct BindingName
        {
            public readonly Key Key;
            public readonly JsString StringValue;

            public BindingName(string value)
            {
                Key = (Key) value;
                StringValue = JsString.Create(value);
            }
        }
    }
}

