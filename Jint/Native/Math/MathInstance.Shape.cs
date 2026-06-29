using Jint.Collections;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Math;

// B1 pilot: generated built-in shape for MathInstance.
//
// Instead of eagerly building a per-realm StringDictionarySlim of ~45 PropertyDescriptors in
// Initialize() (CreateProperties_Generated), Math shares one immutable layout (names + flags + a
// name->slot index, built once per process) and each realm allocates only a small descriptor array
// that is filled lazily: the 8 immutable numeric constants reuse the process-shared static
// descriptors, and each function descriptor is created on first access (its dispatcher Function was
// already lazily materialized before this change, so nothing is materialized eagerly that wasn't
// already). This removes the per-realm dictionary and the eager lazy-descriptor objects.
//
// Anything the flat layout can't represent — adding a new own property, deleting one — deopts to the
// ordinary dictionary representation (DeoptToDictionary), after which every path falls back to the
// base ObjectInstance behavior unchanged. Attribute redefinition of an existing property mutates the
// per-realm descriptor in place exactly as the dictionary path does, so verifyProperty needs no deopt.
internal sealed partial class MathInstance
{
    // ---- shared, built once per process ----

    // Property names in own-property (insertion) order: the 8 constants first (declaration order),
    // then the functions (generated order) — matching CreateProperties_Generated's emission order.
    private static readonly Key[] _shapeNames;
    // Per slot: the process-shared descriptor for an immutable constant, or null for a function.
    private static readonly PropertyDescriptor?[] _constTemplate;
    // Per slot: the dispatcher Slot to materialize for a function, or NotAFunction for a constant.
    private static readonly ushort[] _funcSlots;
    // name -> slot index.
    private static readonly StringDictionarySlim<int> _shapeIndex;

    private const ushort NotAFunction = ushort.MaxValue;

    private const int ShapeEntryCount = 45; // 8 constants + 37 functions

    static MathInstance()
    {
        _shapeNames = new Key[ShapeEntryCount];
        _constTemplate = new PropertyDescriptor?[ShapeEntryCount];
        _funcSlots = new ushort[ShapeEntryCount];
        _shapeIndex = new StringDictionarySlim<int>(ShapeEntryCount);

        var index = 0;

        void Constant(in Key name, PropertyDescriptor descriptor)
        {
            _shapeNames[index] = name;
            _constTemplate[index] = descriptor;
            _funcSlots[index] = NotAFunction;
            _shapeIndex[name] = index;
            index++;
        }

        void Function(in Key name, __MathInstanceFunction.Slot slot)
        {
            _shapeNames[index] = name;
            _constTemplate[index] = null;
            _funcSlots[index] = (ushort) slot;
            _shapeIndex[name] = index;
            index++;
        }

        // Constants first (declaration order), then functions (generated order) — matching the order
        // CreateProperties_Generated would insert them, so own-property enumeration order is preserved.
        Constant(__Key_E, __MathInstance_Property_EValue);
        Constant(__Key_LN10, __MathInstance_Property_LN10Value);
        Constant(__Key_LN2, __MathInstance_Property_LN2Value);
        Constant(__Key_LOG10E, __MathInstance_Property_LOG10EValue);
        Constant(__Key_LOG2E, __MathInstance_Property_LOG2EValue);
        Constant(__Key_PI, __MathInstance_Property_PIValue);
        Constant(__Key_SQRT1_2, __MathInstance_Property_SQRT1_2Value);
        Constant(__Key_SQRT2, __MathInstance_Property_SQRT2Value);

        Function(__Key_abs, __MathInstanceFunction.Slot.Abs);
        Function(__Key_acos, __MathInstanceFunction.Slot.Acos);
        Function(__Key_acosh, __MathInstanceFunction.Slot.Acosh);
        Function(__Key_asin, __MathInstanceFunction.Slot.Asin);
        Function(__Key_asinh, __MathInstanceFunction.Slot.Asinh);
        Function(__Key_atan, __MathInstanceFunction.Slot.Atan);
        Function(__Key_atan2, __MathInstanceFunction.Slot.Atan2);
        Function(__Key_atanh, __MathInstanceFunction.Slot.Atanh);
        Function(__Key_cbrt, __MathInstanceFunction.Slot.Cbrt);
        Function(__Key_ceil, __MathInstanceFunction.Slot.Ceil);
        Function(__Key_clz32, __MathInstanceFunction.Slot.Clz32);
        Function(__Key_cos, __MathInstanceFunction.Slot.Cos);
        Function(__Key_cosh, __MathInstanceFunction.Slot.Cosh);
        Function(__Key_exp, __MathInstanceFunction.Slot.Exp);
        Function(__Key_expm1, __MathInstanceFunction.Slot.Expm1);
        Function(__Key_f16round, __MathInstanceFunction.Slot.F16Round);
        Function(__Key_floor, __MathInstanceFunction.Slot.Floor);
        Function(__Key_fround, __MathInstanceFunction.Slot.Fround);
        Function(__Key_hypot, __MathInstanceFunction.Slot.Hypot);
        Function(__Key_imul, __MathInstanceFunction.Slot.Imul);
        Function(__Key_log, __MathInstanceFunction.Slot.Log);
        Function(__Key_log10, __MathInstanceFunction.Slot.Log10);
        Function(__Key_log1p, __MathInstanceFunction.Slot.Log1p);
        Function(__Key_log2, __MathInstanceFunction.Slot.Log2);
        Function(__Key_max, __MathInstanceFunction.Slot.Max);
        Function(__Key_min, __MathInstanceFunction.Slot.Min);
        Function(__Key_pow, __MathInstanceFunction.Slot.Pow);
        Function(__Key_random, __MathInstanceFunction.Slot.Random);
        Function(__Key_round, __MathInstanceFunction.Slot.Round);
        Function(__Key_sign, __MathInstanceFunction.Slot.Sign);
        Function(__Key_sin, __MathInstanceFunction.Slot.Sin);
        Function(__Key_sinh, __MathInstanceFunction.Slot.Sinh);
        Function(__Key_sqrt, __MathInstanceFunction.Slot.Sqrt);
        Function(__Key_sumPrecise, __MathInstanceFunction.Slot.SumPrecise);
        Function(__Key_tan, __MathInstanceFunction.Slot.Tan);
        Function(__Key_tanh, __MathInstanceFunction.Slot.Tanh);
        Function(__Key_trunc, __MathInstanceFunction.Slot.Truncate);
    }

    // ---- per-realm storage ----

    // Lazily-filled descriptors, parallel to _shapeNames. Constants point at the shared static
    // descriptors; functions are null until first accessed. Null _descriptors == deopted to _properties.
    private PropertyDescriptor?[]? _descriptors;

    private void InitializeShape()
    {
        _descriptors = (PropertyDescriptor?[]) _constTemplate.Clone();
        CreateSymbols_Generated();
    }

    private PropertyDescriptor MaterializeSlot(int slot)
    {
        var descriptor = _descriptors![slot];
        if (descriptor is null)
        {
            var function = new __MathInstanceFunction(this, (__MathInstanceFunction.Slot) _funcSlots[slot]);
            descriptor = new PropertyDescriptor(function, PropertyFlag.NonEnumerable);
            _descriptors[slot] = descriptor;
        }
        return descriptor;
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        EnsureInitialized();
        var descriptors = _descriptors;
        if (descriptors is not null && property is JsString jsString)
        {
            if (_shapeIndex.TryGetValue(jsString.ToString(), out var slot))
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
        var descriptors = _descriptors;
        if (descriptors is not null && property is JsString jsString)
        {
            if (_shapeIndex.TryGetValue(jsString.ToString(), out var slot))
            {
                // Redefine an existing own property (e.g. data -> accessor) in place; no deopt needed.
                descriptors[slot] = desc;
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
        if (_descriptors is not null && property is JsString jsString && _shapeIndex.ContainsKey(jsString.ToString()))
        {
            DeoptToDictionary();
        }
        return base.Delete(property);
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.String | Types.Symbol)
    {
        EnsureInitialized();
        var descriptors = _descriptors;
        if (descriptors is null)
        {
            return base.GetOwnPropertyKeys(types);
        }

        var keys = new List<JsValue>(_shapeNames.Length + (_symbols?.Count ?? 0));
        if ((types & Types.String) != Types.Empty)
        {
            foreach (var name in _shapeNames)
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
        if (_descriptors is null)
        {
            foreach (var pair in base.GetOwnProperties())
            {
                yield return pair;
            }
            yield break;
        }

        for (var i = 0; i < _shapeNames.Length; i++)
        {
            yield return new KeyValuePair<JsValue, PropertyDescriptor>(JsString.Create(_shapeNames[i].Name), MaterializeSlot(i));
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
    // byte-for-byte what CreateProperties_Generated would have produced.
    private void DeoptToDictionary()
    {
        var descriptors = _descriptors;
        if (descriptors is null)
        {
            return;
        }

        var properties = new PropertyDictionary(_shapeNames.Length, checkExistingKeys: false);
        for (var i = 0; i < _shapeNames.Length; i++)
        {
            properties[_shapeNames[i]] = MaterializeSlot(i);
        }

        _descriptors = null;
        SetProperties(properties); // sets _properties, bumps version (symbols stay in _symbols)
    }
}
