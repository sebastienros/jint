using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// Dynamically constructed JavaScript object instance.
/// </summary>
public sealed class JsObject : ObjectInstance
{
    public JsObject(Engine engine) : base(engine, type: InternalTypes.Object | InternalTypes.PlainObject)
    {
        // A dynamically constructed object has no lazy intrinsic members to populate (Initialize() is the
        // base no-op), so mark it initialized up front. This skips the per-object virtual Initialize() call
        // that EnsureInitialized() would otherwise make on first property access, and lets value-creating
        // fast paths (e.g. CreateDataProperty) run without that overhead.
        _initialized = true;
    }
}
