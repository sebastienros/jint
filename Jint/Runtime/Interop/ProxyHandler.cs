using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime.Descriptors;

namespace Jint.Runtime.Interop;

/// <summary>
/// Allows .NET code to implement ECMAScript Proxy traps. Create proxies via
/// <see cref="Engine.AdvancedOperations.CreateProxy"/> and <see cref="Engine.AdvancedOperations.CreateRevocableProxy"/>.
/// </summary>
/// <remarks>
/// <para>
/// A trap method returning CLR <see langword="null"/> means "trap not implemented — forward to the target",
/// exactly like an absent trap property on a JavaScript handler object. A trap may also decide per-invocation
/// to return <see langword="null"/> to forward that particular operation.
/// </para>
/// <para>
/// All ECMAScript proxy invariants are enforced on trap results, the same way they are for JavaScript
/// handler objects (a lying trap produces a <c>TypeError</c>).
/// </para>
/// <para>
/// A handler instance is not bound to an engine; if shared across engines it must be safe for use from
/// each engine's thread.
/// </para>
/// </remarks>
public abstract class ProxyHandler
{
    /// <summary>
    /// The <c>get</c> trap, https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-get-p-receiver.
    /// Intercepts property reads. Return the property value, or <see langword="null"/> to forward to the target.
    /// Returning <see cref="JsValue.Undefined"/> is a real trap result (the property reads as <c>undefined</c>), not a forward.
    /// </summary>
    /// <remarks>
    /// Note the ECMAScript method-call semantics: <c>proxy.method(x)</c> first fires this <c>get</c> trap
    /// (returning the method) and then performs a plain call on the returned value — the <see cref="Apply"/>
    /// trap only fires when the proxy itself is invoked. To intercept method calls, return a wrapping
    /// function from this trap; memoize the wrappers so that <c>proxy.method === proxy.method</c> holds.
    /// </remarks>
    /// <param name="target">The proxy target.</param>
    /// <param name="property">The canonicalized property key (a <see cref="JsString"/> or <see cref="JsSymbol"/>).</param>
    /// <param name="receiver">The receiver of the read, usually the proxy itself (differs e.g. for <c>Reflect.get</c> with an explicit receiver).</param>
    public virtual JsValue? Get(ObjectInstance target, JsValue property, JsValue receiver) => null;

    /// <summary>
    /// The <c>set</c> trap, https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-set-p-v-receiver.
    /// Intercepts property writes. Return <see langword="true"/> when the write succeeded, <see langword="false"/>
    /// to reject it (a <c>TypeError</c> in strict mode code), or <see langword="null"/> to forward to the target.
    /// </summary>
    /// <param name="target">The proxy target.</param>
    /// <param name="property">The canonicalized property key (a <see cref="JsString"/> or <see cref="JsSymbol"/>).</param>
    /// <param name="value">The value being written.</param>
    /// <param name="receiver">The receiver of the write, usually the proxy itself.</param>
    public virtual bool? Set(ObjectInstance target, JsValue property, JsValue value, JsValue receiver) => null;

    /// <summary>
    /// The <c>has</c> trap, https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-hasproperty-p.
    /// Intercepts the <c>in</c> operator and other <c>[[HasProperty]]</c> probes. Return whether the property
    /// should be reported as present, or <see langword="null"/> to forward to the target.
    /// </summary>
    /// <param name="target">The proxy target.</param>
    /// <param name="property">The canonicalized property key (a <see cref="JsString"/> or <see cref="JsSymbol"/>).</param>
    public virtual bool? Has(ObjectInstance target, JsValue property) => null;

    /// <summary>
    /// The <c>deleteProperty</c> trap, https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-delete-p.
    /// Intercepts the <c>delete</c> operator. Return whether the deletion succeeded, or <see langword="null"/>
    /// to forward to the target.
    /// </summary>
    /// <param name="target">The proxy target.</param>
    /// <param name="property">The canonicalized property key (a <see cref="JsString"/> or <see cref="JsSymbol"/>).</param>
    public virtual bool? DeleteProperty(ObjectInstance target, JsValue property) => null;

    /// <summary>
    /// The <c>getOwnPropertyDescriptor</c> trap, https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-getownproperty-p.
    /// Return a descriptor for the property, <see cref="PropertyDescriptor.Undefined"/> to report that no such
    /// own property exists, or <see langword="null"/> to forward to the target.
    /// </summary>
    /// <param name="target">The proxy target.</param>
    /// <param name="property">The canonicalized property key (a <see cref="JsString"/> or <see cref="JsSymbol"/>).</param>
    public virtual PropertyDescriptor? GetOwnPropertyDescriptor(ObjectInstance target, JsValue property) => null;

    /// <summary>
    /// The <c>defineProperty</c> trap, https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-defineownproperty-p-desc.
    /// Intercepts <c>Object.defineProperty</c> and friends. Return whether the definition succeeded,
    /// or <see langword="null"/> to forward to the target.
    /// </summary>
    /// <param name="target">The proxy target.</param>
    /// <param name="property">The canonicalized property key (a <see cref="JsString"/> or <see cref="JsSymbol"/>).</param>
    /// <param name="descriptor">The descriptor being defined.</param>
    public virtual bool? DefineProperty(ObjectInstance target, JsValue property, PropertyDescriptor descriptor) => null;

    /// <summary>
    /// The <c>ownKeys</c> trap, https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-ownpropertykeys.
    /// Intercepts own-key enumeration (<c>Object.keys</c>, <c>Object.getOwnPropertyNames</c>, spread, <c>for...in</c>, ...).
    /// Return the list of own property keys (each a <see cref="JsString"/> or <see cref="JsSymbol"/>, without duplicates),
    /// or <see langword="null"/> to forward to the target.
    /// </summary>
    /// <param name="target">The proxy target.</param>
    public virtual IReadOnlyList<JsValue>? OwnKeys(ObjectInstance target) => null;

    /// <summary>
    /// The <c>apply</c> trap, https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-call-thisargument-argumentslist.
    /// Intercepts calling the proxy itself (only reachable when the target is callable, per spec).
    /// Return the call result, or <see langword="null"/> to forward the call to the target.
    /// Note that <c>proxy.method(x)</c> does not fire this trap — see <see cref="Get"/>.
    /// </summary>
    /// <param name="target">The proxy target.</param>
    /// <param name="thisObject">The <c>this</c> value of the call.</param>
    /// <param name="arguments">The call arguments; the array is a copy private to this invocation.</param>
    public virtual JsValue? Apply(ObjectInstance target, JsValue thisObject, JsValue[] arguments) => null;

    /// <summary>
    /// The <c>construct</c> trap, https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-construct-argumentslist-newtarget.
    /// Intercepts <c>new proxy(...)</c> (only reachable when the target is a constructor, per spec).
    /// Return the constructed object, or <see langword="null"/> to forward to the target constructor.
    /// </summary>
    /// <param name="target">The proxy target.</param>
    /// <param name="arguments">The constructor arguments; the array is a copy private to this invocation.</param>
    /// <param name="newTarget">The <c>new.target</c> value.</param>
    public virtual ObjectInstance? Construct(ObjectInstance target, JsValue[] arguments, JsValue newTarget) => null;

    /// <summary>
    /// The <c>getPrototypeOf</c> trap, https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-getprototypeof.
    /// Return the prototype object, <see cref="JsValue.Null"/> to report a <c>null</c> prototype,
    /// or CLR <see langword="null"/> to forward to the target.
    /// </summary>
    /// <param name="target">The proxy target.</param>
    public virtual JsValue? GetPrototypeOf(ObjectInstance target) => null;

    /// <summary>
    /// The <c>setPrototypeOf</c> trap, https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-setprototypeof-v.
    /// Return whether the prototype change succeeded, or <see langword="null"/> to forward to the target.
    /// </summary>
    /// <param name="target">The proxy target.</param>
    /// <param name="prototype">The new prototype: an object or <see cref="JsValue.Null"/>.</param>
    public virtual bool? SetPrototypeOf(ObjectInstance target, JsValue prototype) => null;

    /// <summary>
    /// The <c>isExtensible</c> trap, https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-isextensible.
    /// Return whether the object is extensible (the result must match the target's actual extensibility, per spec),
    /// or <see langword="null"/> to forward to the target.
    /// </summary>
    /// <param name="target">The proxy target.</param>
    public virtual bool? IsExtensible(ObjectInstance target) => null;

    /// <summary>
    /// The <c>preventExtensions</c> trap, https://tc39.es/ecma262/#sec-proxy-object-internal-methods-and-internal-slots-preventextensions.
    /// Return whether extensions were prevented (returning <see langword="true"/> requires the target to actually
    /// be non-extensible, per spec), or <see langword="null"/> to forward to the target.
    /// </summary>
    /// <param name="target">The proxy target.</param>
    public virtual bool? PreventExtensions(ObjectInstance target) => null;
}

/// <summary>
/// Result of <see cref="Engine.AdvancedOperations.CreateRevocableProxy"/>, mirroring JavaScript's <c>Proxy.revocable()</c>.
/// </summary>
public readonly struct RevocableProxy
{
    private readonly JsProxy _proxy;

    internal RevocableProxy(JsProxy proxy)
    {
        _proxy = proxy;
    }

    /// <summary>
    /// The proxy object.
    /// </summary>
    public ObjectInstance Proxy => _proxy;

    /// <summary>
    /// Revokes the proxy: subsequent trap operations throw a <c>TypeError</c>. Idempotent.
    /// </summary>
    public void Revoke() => _proxy.Revoke();
}
