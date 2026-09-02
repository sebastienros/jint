using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.DevTools.Domains;

/// <summary>
/// What a client hears when script calls a function the client asked for: <c>Runtime.addBinding</c>'s half
/// of <c>Runtime.bindingCalled</c>.
/// </summary>
/// <remarks>
/// One per attachment, and reference identity is all the registry uses.
/// </remarks>
internal interface IBindingListener
{
    /// <summary>Runs on the engine thread, inside the script call that invoked the binding.</summary>
    /// <param name="name">The binding's name.</param>
    /// <param name="payload">The one argument, as text.</param>
    void BindingCalled(string name, string payload);
}

/// <summary>
/// The global functions <c>Runtime.addBinding</c> installed on one target's engine, and who hears them.
/// </summary>
/// <remarks>
/// <para>
/// A binding is a global function of the client's choosing that takes one argument and, when script calls
/// it, sends that argument back to the client as <c>Runtime.bindingCalled</c>. It is how Puppeteer's
/// <c>exposeFunction</c> and its wait helpers get an answer out of a page, and it is on the recorded path of
/// all three Puppeteer clients.
/// </para>
/// <para>
/// <b>The function is installed once and heard by everyone who asked.</b> Two attachments adding the same
/// name share the one global — installing a second would silently replace the first and break whichever
/// client got there first — and both are told when it is called. A repeat from the same attachment is a
/// success that installs nothing, which is what Chrome does.
/// </para>
/// <para>
/// <b>Which context a binding belongs to.</b> The protocol scopes a binding by execution context, by
/// identifier or by name. An engine target has exactly one context and no isolated worlds, so a scoped
/// request lands on that one context rather than on nothing: a binding a client cannot call is worse than a
/// binding wider than it asked for, and the client asked for a name this target has no second candidate for.
/// An <c>executionContextId</c> naming a context that is not this one is refused, because that is a client
/// addressing a context that has gone.
/// </para>
/// <para>
/// Adding and removing run on the engine thread, because both touch the global object. Dropping a listener
/// — what detaching does — runs anywhere, because it touches nothing but a list.
/// </para>
/// </remarks>
internal sealed class BindingRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, List<IBindingListener>> _listeners = new(StringComparer.Ordinal);

    /// <summary>
    /// Installs every registered binding on <paramref name="engine"/>, which a navigation owes them.
    /// </summary>
    /// <param name="engine">The engine of the document about to run. Called on that engine's thread.</param>
    /// <remarks>
    /// <b>Before any script of the new document runs.</b> A client adds a binding and then expects the page
    /// it is about to load to be able to call it — that is what <c>Runtime.addBinding</c> is for, and it is
    /// how Puppeteer's <c>exposeFunction</c> survives a navigation. The registry is the target's, so this is
    /// the one place where the two halves meet.
    /// </remarks>
    internal void Reinstall(Engine engine)
    {
        string[] names;

        lock (_gate)
        {
            if (_listeners.Count == 0)
            {
                return;
            }

            names = new string[_listeners.Count];
            _listeners.Keys.CopyTo(names, 0);
        }

        foreach (var name in names)
        {
            Install(engine, name);
        }
    }

    /// <summary>Installs the binding <paramref name="name"/> if it is not there, and subscribes <paramref name="listener"/>.</summary>
    /// <param name="engine">The engine whose global carries the function. Called on that engine's thread.</param>
    /// <param name="name">The function's name.</param>
    /// <param name="listener">Who to tell when script calls it.</param>
    internal void Add(Engine engine, string name, IBindingListener listener)
    {
        bool install;

        lock (_gate)
        {
            if (!_listeners.TryGetValue(name, out var subscribers))
            {
                _listeners[name] = subscribers = [];
            }

            install = subscribers.Count == 0;

            if (!subscribers.Contains(listener))
            {
                subscribers.Add(listener);
            }
        }

        if (install)
        {
            Install(engine, name);
        }
    }

    /// <summary>Puts one binding's function on <paramref name="engine"/>'s global.</summary>
    private void Install(Engine engine, string name)
    {
        engine.SetValue(name, new ClrFunction(engine, name, (_, arguments) =>
        {
            // Chrome refuses anything but one string here. This coerces instead: minting the realm's own
            // TypeError is not something a package outside the engine assembly can do, and a refusal spelled
            // as anything else would be a different failure from the one a client is written against.
            var payload = arguments.Length > 0 ? TypeConverter.ToString(arguments[0]) : "";
            Notify(name, payload);
            return JsValue.Undefined;
        }, 1));
    }

    /// <summary>
    /// Unsubscribes <paramref name="listener"/> from <paramref name="name"/>, removing the global function
    /// once nobody is listening.
    /// </summary>
    /// <param name="engine">The engine whose global carries the function. Called on that engine's thread.</param>
    /// <param name="name">The function's name.</param>
    /// <param name="listener">Who to stop telling.</param>
    internal void Remove(Engine engine, string name, IBindingListener listener)
    {
        bool uninstall;

        lock (_gate)
        {
            if (!_listeners.TryGetValue(name, out var subscribers) || !subscribers.Remove(listener))
            {
                return;
            }

            uninstall = subscribers.Count == 0;
            if (uninstall)
            {
                _listeners.Remove(name);
            }
        }

        if (uninstall)
        {
            engine.Global.Delete(name);
        }
    }

    /// <summary>
    /// Drops <paramref name="listener"/> from every binding, which is what an attachment going away means.
    /// </summary>
    /// <remarks>
    /// The functions themselves stay on the global: removing one is engine work and detaching happens on a
    /// transport thread, and a global that answers nothing is a far smaller surprise to the script running
    /// in it than a global that vanishes mid-call.
    /// </remarks>
    internal void RemoveAll(IBindingListener listener)
    {
        lock (_gate)
        {
            foreach (var subscribers in _listeners.Values)
            {
                subscribers.Remove(listener);
            }
        }
    }

    private void Notify(string name, string payload)
    {
        IBindingListener[] subscribers;

        lock (_gate)
        {
            if (!_listeners.TryGetValue(name, out var listeners) || listeners.Count == 0)
            {
                return;
            }

            subscribers = listeners.ToArray();
        }

        foreach (var subscriber in subscribers)
        {
            subscriber.BindingCalled(name, payload);
        }
    }
}
