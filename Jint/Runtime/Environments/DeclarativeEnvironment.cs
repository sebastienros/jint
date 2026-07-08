using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native;
using Jint.Native.Disposable;

namespace Jint.Runtime.Environments;

/// <summary>
/// Represents a declarative environment record
/// https://tc39.es/ecma262/#sec-declarative-environment-records
/// </summary>
internal class DeclarativeEnvironment : Environment
{
    internal HybridDictionary<Binding>? _dictionary;
    internal readonly bool _catchEnvironment;
    private DisposeCapability? _disposeCapability;

    // Fixed-slot binding storage for qualifying functions
    internal Binding[]? _slots;
    internal Key[]? _slotNames;

    public DeclarativeEnvironment(Engine engine, bool catchEnvironment = false) : base(engine)
    {
        _catchEnvironment = catchEnvironment;
    }

    private int SlotIndexOf(Key name)
    {
        var slotNames = _slotNames!;
        for (var i = 0; i < slotNames.Length; i++)
        {
            if (slotNames[i] == name) return i;
        }
        return -1;
    }

    /// <summary>
    /// Materializes an unboxed number binding with write-back caching so subsequent reads
    /// return the same instance, or returns null when the binding is uninitialized (TDZ).
    /// Cold by design: it runs at most once per unboxed write.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static JsValue? MaterializeUnboxedOrNull(ref Binding binding)
    {
        if (!binding.IsUnboxedNumber)
        {
            return null;
        }

        var value = JsNumber.Create(binding.UnboxedNumber);
        binding = binding.ChangeValue(value);
        return value;
    }

    /// <summary>
    /// Reads a slot-stored binding as a raw number for the numeric read-modify-write fast
    /// paths. Succeeds only for initialized, mutable bindings currently holding a number
    /// (unboxed or as a JsNumber), so that <see cref="SetNumberSlot"/> may store unchecked.
    /// </summary>
    internal bool TryGetNumberSlot(int slotIndex, out double value)
    {
        // bounds-checked like the identifier slot-cache read path: a cached index is
        // deterministic per AST node, but a stale/torn cache must fall back, not throw
        var slots = _slots;
        if (slots is null || (uint) slotIndex >= (uint) slots.Length)
        {
            value = 0;
            return false;
        }

        ref var binding = ref slots[slotIndex];
        if (binding.Mutable)
        {
            if (binding.IsUnboxedNumber)
            {
                value = binding.UnboxedNumber;
                return true;
            }

            if (binding.HasReferenceValue && binding.Value is JsNumber number)
            {
                value = number._value;
                return true;
            }
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Read-only sibling of <see cref="TryGetNumberSlot"/> for the comparison lanes: admits
    /// immutable bindings too (a `const` number is the canonical loop-body operand). Uninitialized
    /// (TDZ) bindings have neither an unboxed number nor a reference value, so they decline here
    /// and the generic path raises the ReferenceError. Never pair with <see cref="SetNumberSlot"/>.
    /// </summary>
    internal bool TryGetNumberSlotForRead(int slotIndex, out double value)
    {
        var slots = _slots;
        if (slots is null || (uint) slotIndex >= (uint) slots.Length)
        {
            value = 0;
            return false;
        }

        ref var binding = ref slots[slotIndex];
        if (binding.IsUnboxedNumber)
        {
            value = binding.UnboxedNumber;
            return true;
        }

        if (binding.HasReferenceValue && binding.Value is JsNumber number)
        {
            value = number._value;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Stores a raw number into a slot previously validated by <see cref="TryGetNumberSlot"/>
    /// without materializing a JsNumber.
    /// </summary>
    internal void SetNumberSlot(int slotIndex, double value)
    {
        ref var binding = ref _slots![slotIndex];
        Debug.Assert(binding.Mutable && binding.IsInitialized());
        binding = binding.WithUnboxedNumber(value);
    }

    /// <summary>
    /// Finds the fixed slot index for a name so callers can cache the location
    /// (same lifetime guarantees as the identifier slot-binding cache).
    /// </summary>
    internal int FindSlotIndex(Key name)
    {
        if (_slots is not null && _slotNames is not null)
        {
            return SlotIndexOf(name);
        }

        return -1;
    }

    internal sealed override bool HasBinding(BindingName name) => HasBinding(name.Key);

    internal sealed override bool HasBinding(Key name)
    {
        if (_slots is not null && SlotIndexOf(name) >= 0)
        {
            return true;
        }
        return _dictionary is not null && _dictionary.ContainsKey(name);
    }

    internal override bool TryGetBinding(BindingName name, bool strict, [NotNullWhen(true)] out JsValue? value)
    {
        if (_slots is not null)
        {
            var index = SlotIndexOf(name.Key);
            if (index >= 0)
            {
                ref var binding = ref _slots[index];
                value = binding.HasReferenceValue ? binding.Value : MaterializeUnboxedOrNull(ref binding)!;
                return true;
            }
        }

        if (_dictionary?.TryGetValue(name.Key, out var dictionaryBinding) == true)
        {
            value = dictionaryBinding.Value;
            return true;
        }

        value = null;
        return false;
    }

    internal void CreateMutableBindingAndInitialize(Key name, bool canBeDeleted, JsValue value, DisposeHint hint)
    {
        if (_slots is not null)
        {
            var index = SlotIndexOf(name);
            if (index >= 0)
            {
                _slots[index] = new Binding(value, canBeDeleted, mutable: true, strict: false);
                if (hint != DisposeHint.Normal)
                {
                    HandleDisposal(value, hint);
                }
                return;
            }
        }
        _dictionary ??= new HybridDictionary<Binding>();
        _dictionary[name] = new Binding(value, canBeDeleted, mutable: true, strict: false);
        if (hint != DisposeHint.Normal)
        {
            HandleDisposal(value, hint);
        }
    }

    internal void CreateImmutableBindingAndInitialize(Key name, bool strict, JsValue value, DisposeHint hint)
    {
        if (_slots is not null)
        {
            var index = SlotIndexOf(name);
            if (index >= 0)
            {
                _slots[index] = new Binding(value, canBeDeleted: false, mutable: false, strict);
                if (hint != DisposeHint.Normal)
                {
                    HandleDisposal(value, hint);
                }
                return;
            }
        }
        _dictionary ??= new HybridDictionary<Binding>();
        _dictionary[name] = new Binding(value, canBeDeleted: false, mutable: false, strict);
        if (hint != DisposeHint.Normal)
        {
            HandleDisposal(value, hint);
        }
    }

    internal sealed override void CreateMutableBinding(Key name, bool canBeDeleted = false)
    {
        if (_slots is not null)
        {
            var index = SlotIndexOf(name);
            if (index >= 0)
            {
                _slots[index] = new Binding(null!, canBeDeleted, mutable: true, strict: false);
                return;
            }
        }
        _dictionary ??= new HybridDictionary<Binding>();
        _dictionary.CreateMutableBinding(name, canBeDeleted);
    }

    internal sealed override void CreateImmutableBinding(Key name, bool strict = true)
    {
        if (_slots is not null)
        {
            var index = SlotIndexOf(name);
            if (index >= 0)
            {
                _slots[index] = new Binding(null!, canBeDeleted: false, mutable: false, strict);
                return;
            }
        }
        _dictionary ??= new HybridDictionary<Binding>();
        _dictionary.CreateImmutableBinding(name, strict);
    }

    /// <summary>
    /// Slot-lane variant of <see cref="InitializeBinding"/> for lexical declarations whose slot
    /// index was already resolved against this environment's slot layout by the caller. Only
    /// valid for let/const targets (DisposeHint.Normal); mirrors the slot arm of
    /// <see cref="InitializeBinding"/> exactly.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void InitializeSlotBinding(int index, JsValue value)
    {
        ref var binding = ref _slots![index];
        binding = binding.ChangeValue(value);
    }

    internal sealed override void InitializeBinding(Key name, JsValue value, DisposeHint hint)
    {
        if (_slots is not null)
        {
            var index = SlotIndexOf(name);
            if (index >= 0)
            {
                ref var binding = ref _slots[index];
                _slots[index] = binding.ChangeValue(value);
                if (hint != DisposeHint.Normal)
                {
                    HandleDisposal(value, hint);
                }
                return;
            }
        }
        _dictionary ??= new HybridDictionary<Binding>();
        _dictionary.SetOrUpdateValue(name, static (current, value) => current.ChangeValue(value), value);
        if (hint != DisposeHint.Normal)
        {
            HandleDisposal(value, hint);
        }
    }

    internal sealed override void SetMutableBinding(BindingName name, JsValue value, bool strict) => SetMutableBinding(name.Key, value, strict);

    internal sealed override void SetMutableBinding(Key name, JsValue value, bool strict)
    {
        if (_slots is not null)
        {
            var index = SlotIndexOf(name);
            if (index >= 0)
            {
                ref var slot = ref _slots[index];
                if (slot.Strict)
                {
                    strict = true;
                }
                if (!slot.IsInitialized())
                {
                    ThrowUninitializedBindingError(name);
                }
                if (slot.Mutable)
                {
                    _slots[index] = slot.ChangeValue(value);
                }
                else if (strict)
                {
                    Throw.TypeError(_engine.Realm, "Assignment to constant variable.");
                }
                return;
            }
        }

        _dictionary ??= new HybridDictionary<Binding>();

        ref var binding = ref _dictionary.GetValueRefOrNullRef(name);
        if (Unsafe.IsNullRef(ref binding))
        {
            if (strict)
            {
                Throw.ReferenceNameError(_engine.Realm, name);
            }

            _dictionary[name] = new Binding(value, canBeDeleted: true, mutable: true, strict: false);
            return;
        }

        if (binding.Strict)
        {
            strict = true;
        }

        // Is it an uninitialized binding?
        if (!binding.IsInitialized())
        {
            ThrowUninitializedBindingError(name);
        }

        if (binding.Mutable)
        {
            binding = binding.ChangeValue(value);
        }
        else
        {
            if (strict)
            {
                Throw.TypeError(_engine.Realm, "Assignment to constant variable.");
            }
        }
    }

    internal override JsValue GetBindingValue(Key name, bool strict)
    {
        if (_slots is not null)
        {
            var index = SlotIndexOf(name);
            if (index >= 0)
            {
                ref var binding = ref _slots[index];
                if (binding.HasReferenceValue)
                {
                    return binding.Value;
                }

                var materialized = MaterializeUnboxedOrNull(ref binding);
                if (materialized is not null)
                {
                    return materialized;
                }

                ThrowUninitializedBindingError(name);
                return null!;
            }
        }

        if (_dictionary is not null && _dictionary.TryGetValue(name, out var dBinding) && dBinding.IsInitialized())
        {
            return dBinding.Value;
        }

        ThrowUninitializedBindingError(name);
        return null!;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUninitializedBindingError(Key name)
    {
        Throw.ReferenceError(_engine.Realm, $"Cannot access '{name}' before initialization");
    }

    internal sealed override bool DeleteBinding(Key name)
    {
        if (_slots is not null)
        {
            var index = SlotIndexOf(name);
            if (index >= 0)
            {
                ref var binding = ref _slots[index];
                if (!binding.CanBeDeleted)
                {
                    return false;
                }
                _slots[index] = default;
                return true;
            }
        }

        if (_dictionary is null || !_dictionary.TryGetValue(name, out var dBinding))
        {
            return true;
        }

        if (!dBinding.CanBeDeleted)
        {
            return false;
        }

        _dictionary.Remove(name);
        return true;
    }

    internal override bool HasThisBinding() => false;

    internal override bool HasSuperBinding() => false;

    internal sealed override JsValue WithBaseObject() => Undefined;

    internal sealed override bool HasBindings() => _slots is not null || _dictionary?.Count > 0;

    /// <inheritdoc />
    internal sealed override string[] GetAllBindingNames()
    {
        var slotCount = _slotNames?.Length ?? 0;
        var dictCount = _dictionary?.Count ?? 0;
        var total = slotCount + dictCount;
        if (total == 0)
        {
            return [];
        }

        var keys = new string[total];
        var n = 0;
        if (_slotNames is not null)
        {
            for (var i = 0; i < _slotNames.Length; i++)
            {
                keys[n++] = _slotNames[i];
            }
        }
        if (_dictionary is not null)
        {
            foreach (var entry in _dictionary)
            {
                keys[n++] = entry.Key;
            }
        }

        return keys;
    }

    internal override JsValue GetThisBinding() => Undefined;

    internal sealed override Completion DisposeResources(Completion c) => _disposeCapability?.DisposeResources(c) ?? c;

    internal sealed override bool HasDisposeResources => _disposeCapability?.HasResources == true;

    /// <summary>
    /// Begin dispose via the state machine. Returns either Done with the final Completion,
    /// or Suspend with a Promise the caller must await before calling
    /// <see cref="ContinueDisposeResources"/>. If no resources are registered, returns
    /// Done immediately.
    /// </summary>
    internal sealed override DisposeStepResult BeginDisposeResources(Completion c)
        => _disposeCapability?.BeginDispose(c) ?? DisposeStepResult.Done(c);

    /// <summary>
    /// Continue dispose after a suspended Await settles. Returns either Done or the next Suspend.
    /// </summary>
    internal sealed override DisposeStepResult ContinueDisposeResources(JsValue awaitResult, bool awaitThrew)
        => _disposeCapability!.ContinueDispose(awaitResult, awaitThrew);

    public void Clear()
    {
        _dictionary = null;
    }

    /// <summary>
    /// Reset for env-pool reuse: drop any using/await-using disposable resources tracked from the previous call.
    /// </summary>
    internal void ClearDisposeCapability()
    {
        _disposeCapability = null;
    }

    internal void TransferTo(List<Key> names, DeclarativeEnvironment env)
    {
        var source = _dictionary!;
        var target = env._dictionary ??= new HybridDictionary<Binding>(names.Count, checkExistingKeys: true);
        for (var j = 0; j < names.Count; j++)
        {
            var bn = names[j];
            source.TryGetValue(bn, out var lastValue);
            target[bn] = new Binding(lastValue.Value, canBeDeleted: false, mutable: true, strict: false);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void HandleDisposal(JsValue value, DisposeHint hint)
    {
        _disposeCapability ??= new DisposeCapability(_engine);
        _disposeCapability.AddDisposableResource(value, hint);
    }
}

internal static class DictionaryExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CreateMutableBinding<T>(this T dictionary, Key name, bool canBeDeleted = false) where T : IEngineDictionary<Key, Binding>
    {
        dictionary[name] = new Binding(null!, canBeDeleted, mutable: true, strict: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CreateImmutableBinding<T>(this T dictionary, Key name, bool strict = true) where T : IEngineDictionary<Key, Binding>
    {
        dictionary[name] = new Binding(null!, canBeDeleted: false, mutable: false, strict);
    }
}
