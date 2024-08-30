using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Jint.Native;

namespace Jint.Runtime.Environments;

/// <summary>
/// Base implementation of an Environment Record
/// https://tc39.es/ecma262/#sec-environment-records
/// </summary>
[DebuggerTypeProxy(typeof(EnvironmentDebugView))]
internal abstract class Environment : JsValue
{
    protected internal readonly Engine _engine;
    protected internal Environment? _outerEnv;

    protected Environment(Engine engine) : base(InternalTypes.ObjectEnvironmentRecord)
    {
        _engine = engine;
    }

    /// <summary>
    /// Determines if an environment record has a binding for an identifier.
    /// </summary>
    /// <param name="name">The identifier of the binding</param>
    /// <returns><c>true</c> if it does and <c>false</c> if it does not.</returns>
    internal abstract bool HasBinding(Key name);

    internal abstract bool HasBinding(BindingName name);

    internal abstract bool TryGetBinding(BindingName name, bool strict, [NotNullWhen(true)] out JsValue? value);

    /// <summary>
    /// Creates a new mutable binding in an environment record.
    /// </summary>
    /// <param name="name">The identifier of the binding.</param>
    /// <param name="canBeDeleted"><c>true</c> if the binding may be subsequently deleted.</param>
    internal abstract void CreateMutableBinding(Key name, bool canBeDeleted = false);

    /// <summary>
    /// Creates a new but uninitialized immutable binding in an environment record.
    /// </summary>
    /// <param name="name">The identifier of the binding.</param>
    /// <param name="strict"><c>false</c> if the binding may used before it's been initialized.</param>
    internal abstract void CreateImmutableBinding(Key name, bool strict = true);

    /// <summary>
    /// Set the value of an already existing but uninitialized binding in an Environment Record.
    /// </summary>
    /// <param name="name">The text of the bound name</param>
    /// <param name="value">The value for the binding.</param>
    internal abstract void InitializeBinding(Key name, JsValue value);

    /// <summary>
    /// Sets the value of an already existing mutable binding in an environment record.
    /// </summary>
    /// <param name="name">The identifier of the binding</param>
    /// <param name="value">The value of the binding.</param>
    /// <param name="strict">The identify strict mode references.</param>
    internal abstract void SetMutableBinding(Key name, JsValue value, bool strict);

    internal abstract void SetMutableBinding(BindingName name, JsValue value, bool strict);

    /// <summary>
    /// Returns the value of an already existing binding from an environment record.
    /// </summary>
    /// <param name="name">The identifier of the binding</param>
    /// <param name="strict">The identify strict mode references.</param>
    /// <return>The value of an already existing binding from an environment record.</return>
    internal abstract JsValue GetBindingValue(Key name, bool strict);

    /// <summary>
    /// Delete a binding from an environment record. The String value N is the text of the bound name If a binding for N exists, remove the binding and return true. If the binding exists but cannot be removed return false. If the binding does not exist return true.
    /// </summary>
    /// <param name="name">The identifier of the binding</param>
    /// <returns><true>true</true> if the deletion is successfull.</returns>
    internal abstract bool DeleteBinding(Key name);

    internal abstract bool HasThisBinding();

    internal abstract bool HasSuperBinding();

    internal abstract JsValue WithBaseObject();

    internal abstract bool HasBindings();

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

    internal abstract JsValue GetThisBinding();

    internal JsValue? NewTarget { get; set; }

    /// <summary>
    /// Helper to cache JsString/Key when environments use different lookups.
    /// </summary>
    [DebuggerDisplay("\"{Key.Name}\"")]
    internal sealed class BindingName
    {
        public readonly Key Key;
        public readonly JsString Value;
        public readonly JsValue? CalculatedValue;

        public BindingName(string value)
        {
            var key = (Key) value;
            Key = key;
            Value = JsString.Create(value);
            if (key == KnownKeys.Undefined)
            {
                CalculatedValue = Undefined;
            }
        }

        public BindingName(JsString value)
        {
            var key = (Key) value.ToString();
            Key = key;
            Value = value;
            if (key == KnownKeys.Undefined)
            {
                CalculatedValue = Undefined;
            }
        }
    }

    private sealed class EnvironmentDebugView
    {
        private readonly Environment _record;

        public EnvironmentDebugView(Environment record)
        {
            _record = record;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public KeyValuePair<JsValue, JsValue>[] Entries
        {
            get
            {
                var bindingNames = _record.GetAllBindingNames();
                var bindings = new KeyValuePair<JsValue, JsValue>[bindingNames.Length];
                var i = 0;
                foreach (var key in bindingNames)
                {
                    bindings[i++] = new KeyValuePair<JsValue, JsValue>(key, _record.GetBindingValue(key, false));
                }
                return bindings;
            }
        }
    }
}
