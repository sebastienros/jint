using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Jint.Collections;
using Jint.Native;

namespace Jint.Runtime.Environments;

/// <summary>
/// Represents a declarative environment record
/// https://tc39.es/ecma262/#sec-declarative-environment-records
/// </summary>
internal class DeclarativeEnvironment : Environment
{
    internal HybridDictionary<Binding>? _dictionary;
    internal readonly bool _catchEnvironment;

    public DeclarativeEnvironment(Engine engine, bool catchEnvironment = false) : base(engine)
    {
        _catchEnvironment = catchEnvironment;
    }

    internal sealed override bool HasBinding(BindingName name) => HasBinding(name.Key);

    internal sealed override bool HasBinding(Key name) => _dictionary is not null && _dictionary.ContainsKey(name);

    internal override bool TryGetBinding(BindingName name, bool strict, [NotNullWhen(true)] out JsValue? value)
    {
        if (_dictionary?.TryGetValue(name.Key, out var binding) == true)
        {
            value = binding.Value;
            return true;
        }

        value = null;
        return false;
    }

    internal void CreateMutableBindingAndInitialize(Key name, bool canBeDeleted, JsValue value)
    {
        _dictionary ??= new HybridDictionary<Binding>();
        _dictionary[name] = new Binding(value, canBeDeleted, mutable: true, strict: false);
    }

    internal void CreateImmutableBindingAndInitialize(Key name, bool strict, JsValue value)
    {
        _dictionary ??= new HybridDictionary<Binding>();
        _dictionary[name] = new Binding(value, canBeDeleted: false, mutable: false, strict);
    }

    internal sealed override void CreateMutableBinding(Key name, bool canBeDeleted = false)
    {
        _dictionary ??= new HybridDictionary<Binding>();
        _dictionary.CreateMutableBinding(name, canBeDeleted);
    }

    internal sealed override void CreateImmutableBinding(Key name, bool strict = true)
    {
        _dictionary ??= new HybridDictionary<Binding>();
        _dictionary.CreateImmutableBinding(name, strict);
    }

    internal sealed override void InitializeBinding(Key name, JsValue value)
    {
        _dictionary ??= new HybridDictionary<Binding>();
        _dictionary.SetOrUpdateValue(name, static (current, value) => current.ChangeValue(value), value);
    }

    internal sealed override void SetMutableBinding(BindingName name, JsValue value, bool strict) => SetMutableBinding(name.Key, value, strict);

    internal sealed override void SetMutableBinding(Key name, JsValue value, bool strict)
    {
        _dictionary ??= new HybridDictionary<Binding>();

        ref var binding = ref _dictionary.GetValueRefOrNullRef(name);
        if (Unsafe.IsNullRef(ref binding))
        {
            if (strict)
            {
                ExceptionHelper.ThrowReferenceNameError(_engine.Realm, name);
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
                ExceptionHelper.ThrowTypeError(_engine.Realm, "Assignment to constant variable.");
            }
        }
    }

    internal override JsValue GetBindingValue(Key name, bool strict)
    {
        if (_dictionary is not null && _dictionary.TryGetValue(name, out var binding) && binding.IsInitialized())
        {
            return binding.Value;
        }

        ThrowUninitializedBindingError(name);
        return null!;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void ThrowUninitializedBindingError(Key name)
    {
        ExceptionHelper.ThrowReferenceError(_engine.Realm, $"Cannot access '{name}' before initialization");
    }

    internal sealed override bool DeleteBinding(Key name)
    {
        if (_dictionary is null || !_dictionary.TryGetValue(name, out var binding))
        {
            return true;
        }

        if (!binding.CanBeDeleted)
        {
            return false;
        }

        _dictionary.Remove(name);

        return true;
    }

    internal override bool HasThisBinding() => false;

    internal override bool HasSuperBinding() => false;

    internal sealed override JsValue WithBaseObject() => Undefined;

    internal sealed override bool HasBindings() => _dictionary?.Count > 0;

    /// <inheritdoc />
    internal sealed override string[] GetAllBindingNames()
    {
        if (_dictionary is null)
        {
            return [];
        }

        var keys = new string[_dictionary.Count];
        var n = 0;
        foreach (var entry in _dictionary)
        {
            keys[n++] = entry.Key;
        }

        return keys;
    }

    internal override JsValue GetThisBinding() => Undefined;

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
}

internal static class DictionaryExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CreateMutableBinding<T>(this T dictionary, Key name, bool canBeDeleted = false) where T : IEngineDictionary<Key, Binding>
    {
        dictionary[name] = new Binding(null!, canBeDeleted, mutable: true, strict: false);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void CreateImmutableBinding<T>(this T dictionary, Key name, bool strict = true)  where T : IEngineDictionary<Key, Binding>
    {
        dictionary[name] = new Binding(null!, canBeDeleted: false, mutable: false, strict);
    }
}
