using System.Runtime.CompilerServices;

namespace Jint.Runtime.Environments;

/// <summary>
/// Where a read site found a slot-stored binding, described by the <em>layout</em> of every environment
/// on the way rather than by the environments themselves: the number of hops, a layout token for each
/// environment passed, and the slot layout of the one holding the binding. A fresh activation of the same
/// function — or a fresh iteration of the same loop — builds new environments with the same layouts, so the
/// path still describes it, where an identity-pinned cache (<see cref="SlotChainMemo"/>, the slot cache's
/// cached environment) misses, walks the chain again and publishes a replacement.
/// </summary>
/// <remarks>
/// <para>
/// Soundness rests on one property: an environment's set of names is a function of its layout token.
/// A slot-backed environment's token is its <c>_slotNames</c> array, which belongs to the function
/// definition or block that created it and is shared by every environment that definition or block creates;
/// a dictionary-backed function environment's token is its function's definition, whose
/// FunctionDeclarationInstantiation binds the same names every time. The mutations that grow a pre-existing
/// environment's name set — sloppy direct eval hoisting a var, Annex B copying a block function — bump
/// <see cref="Engine._envBindingInjectionEpoch"/>, which the path carries, and mark the environment they grew
/// (<see cref="DeclarativeEnvironment._hasInjectedBindings"/>), which then has no token: the epoch covers an
/// injection after the path was built, the mark one before it. So when every environment on the
/// current chain matches the token recorded for its hop, none of the passed ones can own the name (none did
/// when the path was built), and the one at the last hop stores it at the recorded slot.
/// </para>
/// <para>
/// Environments without a token — object environments (<c>with</c>), whose name set can change at any time
/// and whose probes are observable, and dictionary-backed block environments, which do not know what
/// created them — are never part of a path; a read that has to pass one keeps the identity caches.
/// </para>
/// <para>
/// A path is immutable and published through a single reference, so a read never sees half of one. It
/// holds no environment and no engine-affine object other than definitions, which the engine owns anyway.
/// </para>
/// </remarks>
internal sealed class ScopePath
{
    internal const int MaxHops = 3;

    private readonly object? _token0;
    private readonly object? _token1;
    private readonly object? _token2;
    private readonly Key[] _terminalLayout;
    private readonly int _hops;
    private readonly int _injectionEpoch;
    internal readonly int SlotIndex;

    internal int Hops => _hops;

    private ScopePath(int hops, object? token0, object? token1, object? token2, Key[] terminalLayout, int slotIndex, int injectionEpoch)
    {
        _hops = hops;
        _token0 = token0;
        _token1 = token1;
        _token2 = token2;
        _terminalLayout = terminalLayout;
        SlotIndex = slotIndex;
        _injectionEpoch = injectionEpoch;
    }

    /// <summary>
    /// The token whose equality between two environments implies equal name sets, or null when the
    /// environment has none.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static object? LayoutToken(Environment environment)
    {
        if (environment is DeclarativeEnvironment { _hasInjectedBindings: false } declarative)
        {
            if (declarative._slotNames is { } names)
            {
                return names;
            }

            if (declarative is FunctionEnvironment function)
            {
                return function._functionObject?._functionDefinition;
            }
        }

        return null;
    }

    /// <summary>
    /// Describes the chain from <paramref name="start"/> to <paramref name="terminal"/>, which holds
    /// <paramref name="name"/> at <paramref name="slotIndex"/>; null when the chain is longer than
    /// <see cref="MaxHops"/>, passes an environment without a token, or passes one that owns the name.
    /// </summary>
    internal static ScopePath? TryCreate(Engine engine, Environment start, DeclarativeEnvironment terminal, Key name, int slotIndex)
    {
        if (terminal._slotNames is not { } terminalLayout)
        {
            return null;
        }

        object? token0 = null, token1 = null, token2 = null;
        var hops = 0;
        var current = start;
        while (!ReferenceEquals(current, terminal))
        {
            if (hops == MaxHops
                || LayoutToken(current) is not { } token
                || ((DeclarativeEnvironment) current).HasBinding(name))
            {
                return null;
            }

            switch (hops)
            {
                case 0: token0 = token; break;
                case 1: token1 = token; break;
                default: token2 = token; break;
            }

            hops++;
            current = current._outerEnv;
            if (current is null)
            {
                return null;
            }
        }

        return new ScopePath(hops, token0, token1, token2, terminalLayout, slotIndex, engine._envBindingInjectionEpoch);
    }

    /// <summary>
    /// The environment holding the binding on the current chain, or null when the chain no longer matches.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal DeclarativeEnvironment? Resolve(Engine engine, Environment env)
    {
        if (_hops != 0 && !TryFollow(engine, ref env))
        {
            return null;
        }

        return env is DeclarativeEnvironment declarative && ReferenceEquals(declarative._slotNames, _terminalLayout)
            ? declarative
            : null;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool TryFollow(Engine engine, ref Environment env)
    {
        if (engine._envBindingInjectionEpoch != _injectionEpoch || !ReferenceEquals(LayoutToken(env), _token0))
        {
            return false;
        }

        var next = env._outerEnv;
        if (_hops >= 2)
        {
            if (next is null || !ReferenceEquals(LayoutToken(next), _token1))
            {
                return false;
            }

            next = next._outerEnv;
            if (_hops == 3)
            {
                if (next is null || !ReferenceEquals(LayoutToken(next), _token2))
                {
                    return false;
                }

                next = next._outerEnv;
            }
        }

        if (next is null)
        {
            return false;
        }

        env = next;
        return true;
    }
}
