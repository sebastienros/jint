using System.Collections;
using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint;

/// <summary>
/// An <see cref="Options"/> group or registry that stops accepting changes once an engine has read it.
/// </summary>
/// <remarks>
/// Internal, and implemented explicitly by every group, so that <c>MakeReadOnly</c> stays a single verb on
/// <see cref="Options"/> itself: freezing one group and not its siblings would say nothing a host could use.
/// </remarks>
internal interface IOptionsGroup
{
    void SetReadOnly(bool value);
}

public sealed partial class Options
{
    private bool _readOnly;

    /// <summary>
    /// Whether this configuration has been frozen, after which every setter on it and on its groups throws.
    /// </summary>
    /// <remarks>
    /// Modelled on <c>JsonSerializerOptions.IsReadOnly</c>, and true for the same reason: a component has
    /// taken this instance as its configuration and reads it live.
    /// </remarks>
    public bool IsReadOnly => _readOnly;

    /// <summary>
    /// Freezes this configuration, so that a later write throws instead of reaching an engine that already read it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Engine"/> constructor calls this on the instance it keeps, which is what makes an
    /// <see cref="Options"/> object configuration up to that point and a frozen record afterwards. It is
    /// idempotent, and it cascades to every option group and every registry the instance holds — including a
    /// group materialized after the freeze, which is born frozen.
    /// </para>
    /// <para>
    /// A host may call it itself to publish a configured <see cref="Options"/> that nothing downstream can
    /// change. Building further engines from a frozen instance keeps working: an engine only ever reads it.
    /// </para>
    /// </remarks>
    public void MakeReadOnly() => SetReadOnly(true);

    /// <summary>
    /// Cascades the read-only state to every materialized group. Thawing is used in exactly one place —
    /// <see cref="CreateEngineOptions"/>, whose private clone has to be hardened before the engine reads it.
    /// </summary>
    internal void SetReadOnly(bool value)
    {
        _readOnly = value;
        SetReadOnly(_constraints, value);
        SetReadOnly(_parsing, value);
        SetReadOnly(_interop, value);
        SetReadOnly(_debugger, value);
        SetReadOnly(_coverage, value);
        SetReadOnly(_host, value);
        SetReadOnly(_modules, value);
        SetReadOnly(_intl, value);
        SetReadOnly(_temporal, value);
        SetReadOnly(_json, value);
        SetReadOnly(_profiling, value);
#if NET8_0_OR_GREATER
        SetReadOnly(_webApi, value);
#endif
    }

    private static void SetReadOnly(IOptionsGroup? group, bool value) => group?.SetReadOnly(value);

    private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
    {
        if (_readOnly)
        {
            Throw.OptionsReadOnly("Options." + setting);
        }
    }

    public sealed partial class ConstraintOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value)
        {
            _readOnly = value;
            Constraints.SetReadOnly(value);
            ConstraintFactories.SetReadOnly(value);
        }

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly)
            {
                Throw.OptionsReadOnly("Options.Constraints." + setting);
            }
        }
    }

    public sealed partial class ParsingOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly)
            {
                Throw.OptionsReadOnly("Options.Parsing." + setting);
            }
        }
    }

    public sealed partial class InteropOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value)
        {
            _readOnly = value;
            ExtensionMethodTypes.SetReadOnly(value);
            ObjectConverters.SetReadOnly(value);
            ImmutableCrossingTypes.SetReadOnly(value);
            AllowedAssemblies.SetReadOnly(value);
        }

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly)
            {
                Throw.OptionsReadOnly("Options.Interop." + setting);
            }
        }
    }

    public sealed partial class DebuggerOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly)
            {
                Throw.OptionsReadOnly("Options.Debugger." + setting);
            }
        }
    }

    public sealed partial class CoverageOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly)
            {
                Throw.OptionsReadOnly("Options.Coverage." + setting);
            }
        }
    }

    public sealed partial class HostOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly)
            {
                Throw.OptionsReadOnly("Options.Host." + setting);
            }
        }
    }

    public sealed partial class ModuleOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly)
            {
                Throw.OptionsReadOnly("Options.Modules." + setting);
            }
        }
    }

    public sealed partial class JsonOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly)
            {
                Throw.OptionsReadOnly("Options.Json." + setting);
            }
        }
    }

    public sealed partial class IntlOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly)
            {
                Throw.OptionsReadOnly("Options.Intl." + setting);
            }
        }
    }

    public sealed partial class TemporalOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly)
            {
                Throw.OptionsReadOnly("Options.Temporal." + setting);
            }
        }
    }

    public sealed partial class ProfilingOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly)
            {
                Throw.OptionsReadOnly("Options.Profiling." + setting);
            }
        }
    }

#if NET8_0_OR_GREATER
    /// <summary>
    /// The group <c>Engine.Advanced.EnableWebApis</c> is running a host's configuration callback against on
    /// this thread, which is the one sanctioned write to an engine's own options after construction.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Thread-local rather than a flag toggled on the group, deliberately. The callback is handed the engine's
    /// own <c>WebApi</c> group, and that group may belong to an <see cref="Options"/> instance other engines
    /// are being built from at the same moment — including the process-wide instance a parameterless
    /// <c>new Engine()</c> uses. Clearing the group's own read-only flag for the duration would open it to
    /// every thread and let a concurrent engine build close it again mid-callback; suspending the guard on
    /// the calling thread cannot be seen or disturbed by either.
    /// </para>
    /// <para>
    /// It holds the group being configured rather than a depth count, so the suspension is scoped to one
    /// <see cref="Options"/> instance as well as to one thread: a callback that has got hold of some
    /// <i>other</i> <see cref="Options"/> is writing to a frozen object and is refused there, exactly as it
    /// would be outside the callback. <see cref="WebApiOptions.Owns"/> is the membership test, and it
    /// reference-compares against the eight backing fields rather than putting a back-pointer on each
    /// sub-group — so nothing has to be re-parented by <c>Clone</c>, and a sub-group materialized inside the
    /// callback is covered the moment the accessor publishes it.
    /// </para>
    /// <para>
    /// Two things it deliberately does not cover. The other option groups: an
    /// <c>Action&lt;WebApiOptions&gt;</c> cannot reach <c>Interop</c> or <c>Constraints</c> in the first place.
    /// And the registries — a live enable may set a value, never grow an <see cref="OptionsList{T}"/>, because
    /// a registry on a shared <see cref="Options"/> is read by every engine built from it and a value at least
    /// replaces rather than accumulates.
    /// </para>
    /// </remarks>
    [ThreadStatic]
    private static WebApiOptions? _liveWebApiConfigurationTarget;

    private static bool IsConfiguringWebApisLive(IOptionsGroup group)
        => _liveWebApiConfigurationTarget is { } target && target.Owns(group);

    /// <summary>
    /// Suspends the read-only guard for <paramref name="target"/>'s subtree on this thread.
    /// </summary>
    /// <returns>
    /// The suspension this one replaced, so that a callback re-entering <c>EnableWebApis</c> restores it
    /// rather than clearing it.
    /// </returns>
    internal static WebApiOptions? BeginLiveWebApiConfiguration(WebApiOptions target)
    {
        var previous = _liveWebApiConfigurationTarget;
        _liveWebApiConfigurationTarget = target;
        return previous;
    }

    internal static void EndLiveWebApiConfiguration(WebApiOptions? previous)
        => _liveWebApiConfigurationTarget = previous;

    public sealed partial class WebApiOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value)
        {
            _readOnly = value;
            Options.SetReadOnly(_console, value);
            Options.SetReadOnly(_timers, value);
            Options.SetReadOnly(_fetch, value);
            Options.SetReadOnly(_diagnostics, value);
            Options.SetReadOnly(_storage, value);
            Options.SetReadOnly(_cache, value);
            Options.SetReadOnly(_messaging, value);
            Options.SetReadOnly(_workers, value);
        }

        /// <summary>
        /// Whether <paramref name="group"/> is this group or one of its eight sub-groups.
        /// </summary>
        internal bool Owns(IOptionsGroup group)
            => ReferenceEquals(group, this)
                || ReferenceEquals(group, _console)
                || ReferenceEquals(group, _timers)
                || ReferenceEquals(group, _fetch)
                || ReferenceEquals(group, _diagnostics)
                || ReferenceEquals(group, _storage)
                || ReferenceEquals(group, _cache)
                || ReferenceEquals(group, _messaging)
                || ReferenceEquals(group, _workers);

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly && !IsConfiguringWebApisLive(this))
            {
                Throw.OptionsReadOnly("Options.WebApi." + setting);
            }
        }
    }

    public sealed partial class MessagingOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly && !IsConfiguringWebApisLive(this))
            {
                Throw.OptionsReadOnly("Options.WebApi.Messaging." + setting);
            }
        }
    }

    public sealed partial class WorkerOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly && !IsConfiguringWebApisLive(this))
            {
                Throw.OptionsReadOnly("Options.WebApi.Workers." + setting);
            }
        }
    }

    public sealed partial class StorageOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly && !IsConfiguringWebApisLive(this))
            {
                Throw.OptionsReadOnly("Options.WebApi.Storage." + setting);
            }
        }
    }

    public sealed partial class CacheOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly && !IsConfiguringWebApisLive(this))
            {
                Throw.OptionsReadOnly("Options.WebApi.Cache." + setting);
            }
        }
    }

    public sealed partial class FetchOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value)
        {
            _readOnly = value;
            AllowedSchemes.SetReadOnly(value);
        }

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly && !IsConfiguringWebApisLive(this))
            {
                Throw.OptionsReadOnly("Options.WebApi.Fetch." + setting);
            }
        }
    }

    public sealed partial class ConsoleOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly && !IsConfiguringWebApisLive(this))
            {
                Throw.OptionsReadOnly("Options.WebApi.Console." + setting);
            }
        }
    }

    public sealed partial class TimerOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly && !IsConfiguringWebApisLive(this))
            {
                Throw.OptionsReadOnly("Options.WebApi.Timers." + setting);
            }
        }
    }

    public sealed partial class DiagnosticsOptions : IOptionsGroup
    {
        private bool _readOnly;

        void IOptionsGroup.SetReadOnly(bool value) => _readOnly = value;

        private void ThrowIfReadOnly([CallerMemberName] string? setting = null)
        {
            if (_readOnly && !IsConfiguringWebApisLive(this))
            {
                Throw.OptionsReadOnly("Options.WebApi.Diagnostics." + setting);
            }
        }
    }
#endif
}

/// <summary>
/// A registry on an <see cref="Options"/> group — the object converters, the extension-method types, the
/// registered constraints — which refuses every change once the options are read-only.
/// </summary>
/// <remarks>
/// It exists because a plain <see cref="List{T}"/> cannot refuse one: freezing the settings around a registry
/// a host can still <c>Add</c> to would leave the half of the configuration that grows unguarded.
/// <see cref="ICollection{T}.IsReadOnly"/> answers the same question the surrounding
/// <see cref="Options.IsReadOnly"/> does, so the ordinary collection contract already describes it.
/// </remarks>
public sealed class OptionsList<T> : IList<T>, IReadOnlyList<T>
{
    private readonly List<T> _items;
    private readonly string _name;
    private bool _readOnly;

    internal OptionsList(string name) : this(name, new List<T>())
    {
    }

    internal OptionsList(string name, List<T> items)
    {
        _name = name;
        _items = items;
    }

    internal OptionsList<T> Clone() => new(_name, new List<T>(_items));

    internal void SetReadOnly(bool value) => _readOnly = value;

    /// <inheritdoc />
    public int Count => _items.Count;

    /// <summary>
    /// Whether the surrounding <see cref="Options"/> have been made read-only, in which case every change throws.
    /// </summary>
    public bool IsReadOnly => _readOnly;

    /// <inheritdoc />
    public T this[int index]
    {
        get => _items[index];
        set
        {
            ThrowIfReadOnly();
            _items[index] = value;
        }
    }

    /// <inheritdoc />
    public void Add(T item)
    {
        ThrowIfReadOnly();
        _items.Add(item);
    }

    /// <summary>
    /// Appends every element of <paramref name="items"/>.
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        ThrowIfReadOnly();
        _items.AddRange(items);
    }

    /// <inheritdoc />
    public void Clear()
    {
        ThrowIfReadOnly();
        _items.Clear();
    }

    /// <inheritdoc />
    public void Insert(int index, T item)
    {
        ThrowIfReadOnly();
        _items.Insert(index, item);
    }

    /// <inheritdoc />
    public bool Remove(T item)
    {
        ThrowIfReadOnly();
        return _items.Remove(item);
    }

    /// <inheritdoc />
    public void RemoveAt(int index)
    {
        ThrowIfReadOnly();
        _items.RemoveAt(index);
    }

    /// <summary>
    /// Removes every element matching <paramref name="match"/> and answers how many were removed.
    /// </summary>
    public int RemoveAll(Predicate<T> match)
    {
        ThrowIfReadOnly();
        return _items.RemoveAll(match);
    }

    /// <inheritdoc />
    public bool Contains(T item) => _items.Contains(item);

    /// <inheritdoc />
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    /// <inheritdoc />
    public int IndexOf(T item) => _items.IndexOf(item);

    /// <summary>
    /// Copies the registry into a new array.
    /// </summary>
    public T[] ToArray() => _items.ToArray();

    /// <summary>
    /// Returns an allocation-free enumerator over the registry.
    /// </summary>
    public List<T>.Enumerator GetEnumerator() => _items.GetEnumerator();

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    private void ThrowIfReadOnly()
    {
        if (_readOnly)
        {
            Throw.OptionsReadOnly(_name);
        }
    }
}
