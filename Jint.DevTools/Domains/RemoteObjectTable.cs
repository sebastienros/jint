using System.Globalization;
using Jint.DevTools.Protocol;
using Jint.Native;

namespace Jint.DevTools.Domains;

/// <summary>
/// The handles one engine target has handed out: an <c>objectId</c> a client may come back with, and the
/// <see cref="JsValue"/> it keeps alive until the client lets go.
/// </summary>
/// <remarks>
/// <para>
/// <b>A handle is a promise.</b> Minting one says the value will still be there when the client sends the
/// next command about it, so the table holds every registered value <i>strongly</i>. Nothing here expires,
/// and nothing here is weak: a client that was handed an identifier and found it dangling is worse off than
/// one that was told the value by description.
/// </para>
/// <para>
/// <b>The table belongs to the engine, not to a session</b>, because the values in it are that engine's.
/// Two sessions attached to one target therefore mint identifiers from the same sequence, and a handle one
/// session minted is resolvable by the other — which is what makes an identifier meaningful in the
/// <c>Runtime</c> and <c>Debugger</c> domains of both. Ownership is tracked all the same, so that a session
/// going away releases what it registered and only that.
/// </para>
/// <para>
/// Five ways a handle ends, and the client controls the first three: <c>Runtime.releaseObject</c>,
/// <c>Runtime.releaseObjectGroup</c>, and detaching. The other two are the target being closed and the
/// engine being replaced under it — a navigation, after which the client is told the object cannot be
/// found rather than answered about a value of the document that replaced it.
/// </para>
/// <para>
/// <b>Threading.</b> Every registration and every resolution happens on the engine thread, because both
/// travel with a <see cref="JsValue"/>. The one exception is release: a session detaching is a transport
/// thread's business, and dropping a reference runs no engine code at all. That is the whole reason for the
/// lock — it guards a dictionary, never an engine.
/// </para>
/// </remarks>
internal sealed class RemoteObjectTable
{
    /// <summary>The object group a value with no group of its own belongs to, which is no group.</summary>
    private const string NoGroup = "";

    private readonly string _prefix;
    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    private int _next;

    /// <summary>Creates the table for the runtime numbered <paramref name="runtimeSerial"/>.</summary>
    /// <param name="runtimeSerial">
    /// The runtime's position among the runtimes this process has built. It prefixes every identifier so that
    /// a handle from another target — or from the document before last on this one — is refused rather than
    /// resolving to a value of the wrong engine.
    /// </param>
    internal RemoteObjectTable(int runtimeSerial)
    {
        _prefix = runtimeSerial.ToString(CultureInfo.InvariantCulture) + ".";
    }

    /// <summary>Gets how many handles are outstanding, which is what a leak test counts.</summary>
    internal int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    /// <summary>Registers <paramref name="value"/> and mints the identifier a client addresses it by.</summary>
    /// <param name="owner">
    /// The attachment that asked, whose detaching releases this handle. Reference identity only; the table
    /// never calls anything on it.
    /// </param>
    /// <param name="value">The value to keep alive.</param>
    /// <param name="objectGroup">
    /// The symbolic group <c>Runtime.releaseObjectGroup</c> frees, or <see langword="null"/> for none.
    /// </param>
    /// <remarks>
    /// A fresh identifier every time, even for a value already registered. V8 does the same, and it is what
    /// makes a client's release of one handle not silently invalidate another it still holds.
    /// </remarks>
    internal string Register(object owner, JsValue value, string? objectGroup)
    {
        lock (_gate)
        {
            var id = _prefix + (++_next).ToString(CultureInfo.InvariantCulture);
            _entries.Add(id, new Entry(owner, value, objectGroup ?? NoGroup));
            return id;
        }
    }

    /// <summary>Answers the value <paramref name="objectId"/> names, in Chrome's wording when there is none.</summary>
    /// <exception cref="ProtocolException">
    /// <c>-32000 Could not find object with given id</c>, which is what a client matches on to decide a
    /// handle it holds has gone rather than that it called the command wrongly.
    /// </exception>
    internal JsValue Resolve(string objectId) => Resolve(objectId, out _);

    /// <summary>Answers the value <paramref name="objectId"/> names, and the group it was billed to.</summary>
    /// <param name="objectId">The handle the client sent.</param>
    /// <param name="objectGroup">
    /// The group the handle belongs to, or <see langword="null"/> for none. What a command answering
    /// <i>about</i> a handle bills its own new handles to, so that <c>releaseObjectGroup</c> frees the tree
    /// a client walked rather than only its root.
    /// </param>
    /// <exception cref="ProtocolException">
    /// <c>-32000 Could not find object with given id</c>, which is what a client matches on to decide a
    /// handle it holds has gone rather than that it called the command wrongly.
    /// </exception>
    internal JsValue Resolve(string objectId, out string? objectGroup)
    {
        return TryResolve(objectId, out var value, out objectGroup)
            ? value
            : Throw.ServerError<JsValue>("Could not find object with given id");
    }

    /// <summary>Answers the value <paramref name="objectId"/> names, or whether there is one.</summary>
    internal bool TryResolve(string objectId, out JsValue value) => TryResolve(objectId, out value, out _);

    /// <summary>Answers the value <paramref name="objectId"/> names and its group, or whether there is one.</summary>
    internal bool TryResolve(string objectId, out JsValue value, out string? objectGroup)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(objectId, out var entry))
            {
                value = entry.Value;
                objectGroup = entry.Group.Length == 0 ? null : entry.Group;
                return true;
            }
        }

        value = JsValue.Undefined;
        objectGroup = null;
        return false;
    }

    /// <summary>Releases one handle, answering whether there was one.</summary>
    internal bool Release(string objectId)
    {
        lock (_gate)
        {
            return _entries.Remove(objectId);
        }
    }

    /// <summary>Releases every handle <paramref name="owner"/> registered under <paramref name="objectGroup"/>.</summary>
    /// <remarks>
    /// Scoped to the owner rather than to the whole table: object-group names are a client's own vocabulary,
    /// and two clients attached to one engine both use <c>"console"</c>.
    /// </remarks>
    internal void ReleaseGroup(object owner, string objectGroup)
    {
        Remove(entry => ReferenceEquals(entry.Owner, owner) && string.Equals(entry.Group, objectGroup, StringComparison.Ordinal));
    }

    /// <summary>Releases every handle <paramref name="owner"/> registered, which is what detaching means.</summary>
    internal void ReleaseOwner(object owner)
    {
        Remove(entry => ReferenceEquals(entry.Owner, owner));
    }

    /// <summary>Releases everything, which is what replacing or closing the engine means.</summary>
    internal void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
        }
    }

    private void Remove(Func<Entry, bool> predicate)
    {
        lock (_gate)
        {
            List<string>? doomed = null;
            foreach (var pair in _entries)
            {
                if (predicate(pair.Value))
                {
                    (doomed ??= []).Add(pair.Key);
                }
            }

            if (doomed is null)
            {
                return;
            }

            foreach (var id in doomed)
            {
                _entries.Remove(id);
            }
        }
    }

    /// <summary>One outstanding handle: who asked, what it keeps alive, and what frees it in bulk.</summary>
    private sealed record Entry(object Owner, JsValue Value, string Group);
}
