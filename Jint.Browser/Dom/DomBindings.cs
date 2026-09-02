using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Descriptors.Specialized;
using Jint.WebApi;

namespace Jint.Browser.Dom;

/// <summary>
/// The two doors onto the DOM binding: install the interface objects on an engine, and move a value across
/// the boundary in either direction.
/// </summary>
internal static class DomBindings
{
    /// <summary>
    /// Installs every interface object — <c>Node</c>, <c>Element</c>, <c>HTMLDivElement</c>, <c>NodeList</c>,
    /// … — as a lazy, non-clobbering global, so that <c>instanceof</c> works and a script can name an
    /// interface.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Lazy for the same reason every web API global is: an engine that never mentions
    /// <c>HTMLTableCellElement</c> must not pay for its prototype, and there are a hundred and eighty of them.
    /// Non-clobbering for the same reason too — the host's own configuration has already run, and a name it
    /// took is its own. The check probes rather than reads, so a host's own lazy global is not forced into
    /// existence merely by our looking.
    /// </para>
    /// <para>
    /// The attributes are WebIDL's for an
    /// <a href="https://webidl.spec.whatwg.org/#es-interfaces">interface object</a>:
    /// <c>{ writable: true, enumerable: false, configurable: true }</c>.
    /// </para>
    /// <para>
    /// An interface AngleSharp marked <c>[DomNoInterfaceObject]</c> gets no global. Its prototype is still in
    /// the chain and still carries its members; it simply cannot be named, which is what
    /// <c>[LegacyNoInterfaceObject]</c> means.
    /// </para>
    /// </remarks>
    internal static void Install(Engine engine)
    {
        if (engine is null)
        {
            Throw.ArgumentNullException(nameof(engine));
        }

        var realm = DomRealm.Of(engine);

        // The PRINCIPAL realm, deliberately, and not Engine.Realm — the call WebApiRegistration.InstallGlobals
        // makes and for its reason: during construction the two are the same, but this is a public-shaped door
        // callable from anywhere, including a host callback running inside a ShadowRealm, and these globals
        // belong to the engine's own realm and to no other.
        var global = engine._mainRealm.GlobalObject;

        foreach (var definition in DomInterfaces.All)
        {
            if (!definition.HasInterfaceObject)
            {
                continue;
            }

            if (global.HasOwnProperty(WebApiRegistration.NameOf(definition.Name)))
            {
                continue;
            }

            var captured = definition;
            global.SetProperty(
                definition.Name,
                new LazyPropertyDescriptor<DomRealm>(realm, r => r.InterfaceObjectOf(captured), PropertyFlag.NonEnumerable));
        }
    }

    /// <summary>Projects an AngleSharp object into <paramref name="engine"/>.</summary>
    internal static JsValue Wrap(Engine engine, object? value) => DomRealm.Of(engine).Wrap(value);

    /// <summary>
    /// The brand check every generated member starts with: the receiver has to be a wrapper over an
    /// AngleSharp object implementing <typeparamref name="T"/>, or the call is
    /// <a href="https://webidl.spec.whatwg.org/#dfn-create-operation-function">not on this interface</a>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A wrong receiver is a <c>TypeError: Illegal invocation</c>, which is what a browser answers for
    /// <c>Element.prototype.getAttribute.call({})</c>. When the receiver is an object the error is built in
    /// that object's own realm; when it is a primitive there is no realm to reach, and the engine's
    /// realm-free workaround produces the same <c>TypeError</c> at the enclosing statement.
    /// </para>
    /// <para>
    /// It answers the target and the realm together because a member that wraps its result needs both, and one
    /// type test is what the two of them cost here.
    /// </para>
    /// </remarks>
    internal static DomBinding<T> Bind<T>(JsValue thisObject, string member) where T : class
    {
        if (thisObject is IDomWrapper wrapper && wrapper.DomTarget is T target)
        {
            return new DomBinding<T>(target, wrapper.DomRealm);
        }

        IllegalInvocation(thisObject, member);
        return default;
    }

    /// <summary>
    /// The <c>[LegacyLenientThis]</c> variant: a wrong receiver is not an error, so the caller answers
    /// <see langword="undefined"/> from the getter and ignores the set.
    /// </summary>
    internal static bool TryBind<T>(JsValue thisObject, out DomBinding<T> binding) where T : class
    {
        if (thisObject is IDomWrapper wrapper && wrapper.DomTarget is T target)
        {
            binding = new DomBinding<T>(target, wrapper.DomRealm);
            return true;
        }

        binding = default;
        return false;
    }

    /// <summary>
    /// Unwraps an argument that has to be a wrapper over <typeparamref name="T"/>. WebIDL's interface-type
    /// conversion: anything else is a <c>TypeError</c>.
    /// </summary>
    internal static T Argument<T>(JsValue[] arguments, int index, string member) where T : class
    {
        var value = index < arguments.Length ? arguments[index] : JsValue.Undefined;
        if (value is IDomWrapper wrapper && wrapper.DomTarget is T target)
        {
            return target;
        }

        if (value is ObjectInstance instance)
        {
            Throw.TypeError(
                instance.Engine.Realm,
                "Failed to execute '" + member + "': parameter " + (index + 1) + " is not of the expected type.");
        }

        Throw.TypeErrorNoEngine(
            "Failed to execute '" + member + "': parameter " + (index + 1) + " is not of the expected type.");
        return null!;
    }

    /// <summary>The nullable form of <see cref="Argument{T}"/>: <c>null</c> and <c>undefined</c> pass.</summary>
    internal static T? NullableArgument<T>(JsValue[] arguments, int index, string member) where T : class
    {
        var value = index < arguments.Length ? arguments[index] : JsValue.Undefined;
        return value.IsNullOrUndefined() ? null : Argument<T>(arguments, index, member);
    }

    private static void IllegalInvocation(JsValue thisObject, string member)
    {
        var message = "Failed to execute '" + member + "': Illegal invocation";

        if (thisObject is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
    }
}
