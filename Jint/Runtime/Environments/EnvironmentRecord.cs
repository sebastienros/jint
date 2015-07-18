using Jint.Native;
using Jint.Native.Object;

namespace Jint.Runtime.Environments
{
    /// <summary>
    /// Base implementation of an Environment Record
    /// http://www.ecma-international.org/ecma-262/5.1/#sec-10.2.1
    /// </summary>
    public abstract class EnvironmentRecord : ObjectInstance
    {
        protected EnvironmentRecord(Engine engine) : base(engine)
        {
        }

        /// <summary>
        /// Determines if an environment record has a binding for an identifier. 
        /// </summary>
        /// <param name="name">The identifier of the binding</param>
        /// <returns><c>true</c> if it does and <c>false</c> if it does not.</returns>
        public abstract bool HasBinding(string name);

        /// <summary>
        /// Creates a new mutable binding in an environment record.
        /// </summary>
        /// <param name="name">The identifier of the binding.</param>
        /// <param name="canBeDeleted"><c>true</c> if the binding may be subsequently deleted.</param>
        public abstract void CreateMutableBinding(string name, bool canBeDeleted = false);

        /// <summary>
        /// Sets the value of an already existing mutable binding in an environment record. 
        /// </summary>
        /// <param name="name">The identifier of the binding</param>
        /// <param name="value">The value of the binding.</param>
        /// <param name="strict">The identify strict mode references.</param>
        public abstract void SetMutableBinding(string name, JsValue value, bool strict);
        
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

        /// <summary>
        /// Returns the value to use as the <c>this</c> value on calls to function objects that are obtained as binding values from this environment record.
        /// </summary>
        /// <returns>The value to use as <c>this</c>.</returns>
        public abstract JsValue ImplicitThisValue();

        /// <summary>
        /// Returns an array of all the defined binding names
        /// </summary>
        /// <returns>The array of all defined bindings</returns>
        public abstract string[] GetAllBindingNames();
    }
}
