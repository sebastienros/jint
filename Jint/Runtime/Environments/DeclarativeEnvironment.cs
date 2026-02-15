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
                value = _slots[index].Value;
                return value is not null;
            }
        }

        if (_dictionary?.TryGetValue(name.Key, out var binding) == true)
        {
            value = binding.Value;
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
                if (binding.IsInitialized())
                {
                    return binding.Value;
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

    public void Clear()
    {
        _dictionary = null;
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
