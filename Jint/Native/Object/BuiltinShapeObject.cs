using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

/// <summary>
/// Base for built-in objects (Math, JSON, Reflect, ...) that store their string-keyed own properties as
/// a shared immutable <see cref="BuiltinShape"/> plus a per-realm, lazily-filled descriptor array,
/// instead of building a per-realm dictionary of descriptors in <c>Initialize</c>. Immutable constants
/// reuse process-shared descriptors; function descriptors are created on first access (their dispatcher
/// <see cref="Function"/> was already lazy, so nothing is materialized eagerly that wasn't already).
/// <para>
/// Redefining an existing property's attributes mutates the per-realm descriptor in place exactly as the
/// dictionary path does (so test262's verifyProperty needs no deopt); adding or deleting an own property
/// deopts to the ordinary dictionary, after which every path falls back to the unchanged base behavior.
/// </para>
/// </summary>
internal abstract class BuiltinShapeObject : ObjectInstance
{
    // Lazily-filled descriptors, parallel to BuiltinShape.Names: constants point at the shared static
    // descriptors, functions are null until first accessed. Null == deopted to the base _properties.
    private PropertyDescriptor?[]? _builtinDescriptors;

    protected BuiltinShapeObject(Engine engine) : base(engine)
    {
    }

    /// <summary>The shared layout for this built-in (typically a <c>static readonly</c> field).</summary>
    private protected abstract BuiltinShape BuiltinShape { get; }

    /// <summary>Creates the dispatcher function for a function slot (e.g. <c>new __XxxFunction(this, (Slot) slot)</c>).</summary>
    private protected abstract Jint.Native.Function.Function MakeBuiltinFunction(ushort slot);

    /// <summary>Call from the built-in's <c>Initialize</c> (before any symbol setup).</summary>
    private protected void InitializeBuiltinShape()
    {
        _builtinDescriptors = (PropertyDescriptor?[]) BuiltinShape.ConstTemplate.Clone();
    }

    private PropertyDescriptor MaterializeSlot(int slot)
    {
        var descriptors = _builtinDescriptors!;
        var descriptor = descriptors[slot];
        if (descriptor is null)
        {
            var shape = BuiltinShape;
            descriptor = new PropertyDescriptor(MakeBuiltinFunction(shape.FunctionSlots[slot]), shape.FunctionFlags[slot]);
            descriptors[slot] = descriptor;
        }
        return descriptor;
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        EnsureInitialized();
        if (_builtinDescriptors is not null && property is JsString jsString)
        {
            if (BuiltinShape.Index.TryGetValue(jsString.ToString(), out var slot))
            {
                return MaterializeSlot(slot);
            }
            return PropertyDescriptor.Undefined;
        }
        // Symbol key, or already deopted: the base reads _symbols / _properties.
        return base.GetOwnProperty(property);
    }

    protected internal override void SetOwnProperty(JsValue property, PropertyDescriptor desc)
    {
        EnsureInitialized();
        if (_builtinDescriptors is not null && property is JsString jsString)
        {
            if (BuiltinShape.Index.TryGetValue(jsString.ToString(), out var slot))
            {
                // Redefine an existing own property (e.g. data -> accessor) in place; no deopt needed.
                _builtinDescriptors[slot] = desc;
                unchecked { _propertiesVersion++; }
                return;
            }
            // Adding a brand-new own string property can't be expressed in the fixed layout.
            DeoptToDictionary();
        }
        base.SetOwnProperty(property, desc);
    }

    public override bool Delete(JsValue property)
    {
        EnsureInitialized();
        if (_builtinDescriptors is not null && property is JsString jsString && BuiltinShape.Index.ContainsKey(jsString.ToString()))
        {
            DeoptToDictionary();
        }
        return base.Delete(property);
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        EnsureInitialized();
        if (_builtinDescriptors is null)
        {
            return base.GetOwnPropertyKeys(types);
        }

        var names = BuiltinShape.Names;
        var keys = new List<JsValue>(names.Length + (_symbols?.Count ?? 0));
        if ((types & Types.String) != Types.Empty)
        {
            foreach (var name in names)
            {
                keys.Add(JsString.Create(name.Name));
            }
        }
        if ((types & Types.Symbol) != Types.Empty && _symbols is not null)
        {
            foreach (var pair in _symbols)
            {
                keys.Add(pair.Key);
            }
        }
        return keys;
    }

    public override IEnumerable<KeyValuePair<JsValue, PropertyDescriptor>> GetOwnProperties()
    {
        EnsureInitialized();
        if (_builtinDescriptors is null)
        {
            foreach (var pair in base.GetOwnProperties())
            {
                yield return pair;
            }
            yield break;
        }

        var names = BuiltinShape.Names;
        for (var i = 0; i < names.Length; i++)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(JsString.Create(names[i].Name), MaterializeSlot(i));
        }
        if (_symbols is not null)
        {
            foreach (var pair in _symbols)
            {
                yield return new KeyValuePair<JsValue, PropertyDescriptor>(pair.Key, pair.Value);
            }
        }
    }

    // Fall back to the ordinary dictionary representation, materializing every slot so the object is
    // byte-for-byte what a generated property dictionary would have produced.
    private void DeoptToDictionary()
    {
        var descriptors = _builtinDescriptors;
        if (descriptors is null)
        {
            return;
        }

        var names = BuiltinShape.Names;
        var properties = new PropertyDictionary(names.Length, checkExistingKeys: false);
        for (var i = 0; i < names.Length; i++)
        {
            properties[names[i]] = MaterializeSlot(i);
        }

        _builtinDescriptors = null;
        SetProperties(properties); // sets _properties, bumps version (symbols stay in _symbols)
    }
}
