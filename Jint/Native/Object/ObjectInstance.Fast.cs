using System.Runtime.CompilerServices;
using Jint.Runtime.Descriptors;

namespace Jint.Native.Object;

/// <summary>
/// Raw own-property write helpers that bypass the ECMAScript <c>[[Set]]</c> / <c>[[DefineOwnProperty]]</c>
/// pipeline. They are intended for building up an object the host fully controls — populating a freshly
/// created object, or installing members on a host-provided global — and are not a general substitute for
/// <see cref="ObjectInstance.Set(JsValue,JsValue,JsValue)"/> or
/// <see cref="ObjectInstance.DefineOwnProperty"/>. See the individual members for what is skipped.
/// <para>
/// For building a <em>new</em> plain object out of host data, prefer
/// <see cref="JsObject.Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue})"/> or
/// <see cref="JsObject.CreateFromEntries(Engine, ReadOnlySpan{KeyValuePair{string, JsValue}})"/> over
/// populating a fresh <see cref="JsObject"/> through these helpers: those build directly into the
/// hidden-class representation instead of deoptimizing out of it.
/// </para>
/// </summary>
public partial class ObjectInstance
{
    /// <summary>
    /// Stores <paramref name="value"/> as an own property named <paramref name="name"/> on this object,
    /// replacing any own property of that name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The write always lands as an <em>own</em> property on the receiver and therefore <em>shadows</em>
    /// anything of that name inherited from the prototype chain. If the member is — or later becomes —
    /// prototype-resident, this silently creates a shadow rather than updating the inherited member, and a
    /// data descriptor can end up shadowing an inherited accessor. (<c>Error.prototype.stack</c> is a
    /// concrete example of a member that lives on the prototype as an accessor.)
    /// </para>
    /// <para>
    /// No inherited setter is invoked, and no <c>[[DefineOwnProperty]]</c> validation runs — extensibility,
    /// the configurable/writable flags of an existing property, and the data/accessor compatibility rules
    /// are all ignored, and no <c>TypeError</c> can be raised. That is what "fast" means here.
    /// </para>
    /// <para>
    /// Storing a raw <see cref="PropertyDescriptor"/> is a dictionary-mode operation, so a shape-mode
    /// receiver is deoptimized and permanently forfeits the shape inline cache used for property reads and
    /// writes. Prefer this for setup-time writes only; for steady-state mutation of an existing property use
    /// <see cref="ObjectInstance.Set(JsValue,JsValue,JsValue)"/>, which stores through the existing
    /// descriptor and leaves the receiver's layout intact.
    /// </para>
    /// </remarks>
    public void FastSetProperty(string name, PropertyDescriptor value)
    {
        SetProperty(name, value);
    }

    /// <summary>
    /// Stores <paramref name="value"/> as an own property keyed by <paramref name="property"/> on this
    /// object, replacing any own property of that name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The write always lands as an <em>own</em> property on the receiver and therefore <em>shadows</em>
    /// anything of that name inherited from the prototype chain. If the member is — or later becomes —
    /// prototype-resident, this silently creates a shadow rather than updating the inherited member, and a
    /// data descriptor can end up shadowing an inherited accessor. (<c>Error.prototype.stack</c> is a
    /// concrete example of a member that lives on the prototype as an accessor.)
    /// </para>
    /// <para>
    /// No inherited setter is invoked, and no <c>[[DefineOwnProperty]]</c> validation runs — extensibility,
    /// the configurable/writable flags of an existing property, and the data/accessor compatibility rules
    /// are all ignored, and no <c>TypeError</c> can be raised. That is what "fast" means here.
    /// </para>
    /// <para>
    /// Storing a raw <see cref="PropertyDescriptor"/> under a string key is a dictionary-mode operation, so a
    /// shape-mode receiver is deoptimized and permanently forfeits the shape inline cache used for property
    /// reads and writes. Prefer this for setup-time writes only; for steady-state mutation of an existing
    /// property use <see cref="ObjectInstance.Set(JsValue,JsValue,JsValue)"/>, which stores through the
    /// existing descriptor and leaves the receiver's layout intact.
    /// </para>
    /// </remarks>
    public void FastSetProperty(JsValue property, PropertyDescriptor value)
    {
        SetProperty(property, value);
    }

    /// <summary>
    /// Stores <paramref name="value"/> as a configurable, enumerable, writable own data property named
    /// <paramref name="name"/> on this object, replacing any own property of that name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The write always lands as an <em>own</em> property on the receiver and therefore <em>shadows</em>
    /// anything of that name inherited from the prototype chain. If the member is — or later becomes —
    /// prototype-resident, this silently creates a shadow rather than updating the inherited member, and
    /// the data property written here can end up shadowing an inherited accessor. (<c>Error.prototype.stack</c>
    /// is a concrete example of a member that lives on the prototype as an accessor.)
    /// </para>
    /// <para>
    /// No inherited setter is invoked, and no <c>[[DefineOwnProperty]]</c> validation runs — extensibility,
    /// the configurable/writable flags of an existing property, and the data/accessor compatibility rules
    /// are all ignored, and no <c>TypeError</c> can be raised. That is what "fast" means here.
    /// </para>
    /// <para>
    /// It stores a raw <see cref="PropertyDescriptor"/>, which is a dictionary-mode operation, so a shape-mode
    /// receiver is deoptimized and permanently forfeits the shape inline cache used for property reads and
    /// writes. Prefer this for setup-time writes only; for steady-state mutation of an existing property use
    /// <see cref="ObjectInstance.Set(JsValue,JsValue,JsValue)"/>, which stores through the existing
    /// descriptor and leaves the receiver's layout intact.
    /// </para>
    /// <para>
    /// Despite the name, a loop of these calls is <em>not</em> the fastest way to project host data into a
    /// new object, precisely because of that deopt: each object gets a descriptor per property and a
    /// property dictionary, and the script reading a batch of them never keeps a monomorphic inline cache.
    /// Use <see cref="JsObject.Create(Engine, JsObjectLayout, ReadOnlySpan{JsValue})"/> when the property
    /// names are known up front, or
    /// <see cref="JsObject.CreateFromEntries(Engine, ReadOnlySpan{KeyValuePair{string, JsValue}})"/> when
    /// they are only known at runtime; both build straight into the shaped representation, so objects
    /// sharing a layout share one hidden class.
    /// </para>
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FastSetDataProperty(string name, JsValue value)
    {
        SetProperty(name, new PropertyDescriptor(value, PropertyFlag.ConfigurableEnumerableWritable));
    }
}
