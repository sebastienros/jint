using AngleSharp.Dom;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.Browser.Dom.Collections;

/// <summary>
/// The wrapper for a node whose interface also supports indexed or named properties: <c>form[0]</c>,
/// <c>form.username</c>, <c>select[0]</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why it is a node wrapper with a projection rather than a collection.</b> A node's wrapper is what the
/// engine's tree-dispatch lane keys on, and the wrapper cache keeps exactly one per node, so a form cannot be
/// an <c>ArrayLikeObject</c> without ceasing to be an <c>EventTarget</c> the dispatcher can walk. The
/// projection therefore rides on top: three overrides, kept consistent by construction because all three read
/// the same generated <see cref="DomCollectionAccessor"/> at the same instant.
/// </para>
/// <para>
/// <b>What that costs, and what it does not.</b> Overriding <c>GetOwnProperty</c> and not <c>Get</c> keeps the
/// receiver on the engine's <em>ordinary</em> property-access lane, so an inherited member still resolves in
/// one probe and the prototype-method inline cache still serves it; only an own read consults the projection.
/// The elements are read-only and configurable, which is what Web IDL gives a supported index with no setter,
/// so an assignment to <c>form[0]</c> is refused by <c>[[Set]]</c> without this class overriding it.
/// </para>
/// <para>
/// <b>The coherence obligation is the one <c>Jint/Native/Object/AGENTS.md</c> states.</b> A name
/// <see cref="GetOwnProperty"/> answers has to be a name <see cref="GetOwnPropertyKeys"/> lists, or
/// <c>hasOwnProperty</c> and <c>Object.getOwnPropertyNames</c> disagree about the same object. Both read
/// <c>SupportedNames</c>, so the only way to break it is to give the accessor a named getter whose supported
/// names do not include what it answers.
/// </para>
/// </remarks>
internal sealed class DomIndexedNodeObject : DomNodeObject
{
    private readonly DomCollectionAccessor _accessor;

    internal DomIndexedNodeObject(DomRealm realm, DomInterfaceDefinition definition, INode node, DomCollectionAccessor accessor)
        : base(realm, definition, node)
    {
        _accessor = accessor;
    }

    public override PropertyDescriptor GetOwnProperty(JsValue property)
    {
        if (TryProject(property, out var value, out var enumerable))
        {
            // https://webidl.spec.whatwg.org/#legacy-platform-object-getownproperty - a supported index or
            // name is { writable: false, configurable: true }, and the enumerability is the interface's.
            return new PropertyDescriptor(
                value,
                enumerable
                    ? PropertyFlag.NonWritable
                    : PropertyFlag.OnlyConfigurable);
        }

        return base.GetOwnProperty(property);
    }

    protected internal override OwnPropertyProbe ProbeOwnProperty(JsValue property)
    {
        if (TryProject(property, out _, out var enumerable))
        {
            return enumerable ? OwnPropertyProbe.Enumerable : OwnPropertyProbe.NonEnumerable;
        }

        return base.ProbeOwnProperty(property);
    }

    public override List<JsValue> GetOwnPropertyKeys(Types types = Types.Empty | Types.String | Types.Symbol)
    {
        var keys = base.GetOwnPropertyKeys(types);

        if ((types & Types.String) == Types.Empty)
        {
            return keys;
        }

        // https://tc39.es/ecma262/#sec-ordinaryownpropertykeys - the integer-index keys come first and in
        // ascending order, so they are inserted ahead of everything the base listed rather than appended.
        var length = _accessor.Length(Node);
        var indices = new List<JsValue>((int) System.Math.Min(length, int.MaxValue));
        for (var i = 0u; i < length; i++)
        {
            indices.Add(JsString.Create(i));
        }

        foreach (var name in _accessor.SupportedNames(Node))
        {
            // A name the object already carries - an expando, or an inherited member shadowed by one - is the
            // base list's, and listing it twice would make Object.getOwnPropertyNames report a duplicate.
            if (!keys.Contains(JsString.Create(name)))
            {
                indices.Add(JsString.Create(name));
            }
        }

        indices.AddRange(keys);
        return indices;
    }

    /// <summary>
    /// The one place the projection is read, so the three overrides above can never disagree about it. An
    /// own property the object really has wins, which is what makes an expando assigned over a supported name
    /// behave the way it does on any other object.
    /// </summary>
    private bool TryProject(JsValue property, out JsValue value, out bool enumerable)
    {
        value = JsValue.Undefined;
        enumerable = true;

        if (!property.IsString())
        {
            return false;
        }

        var key = property.ToString();

        if (IsArrayIndex(key, out var index))
        {
            return _accessor.TryGetIndex(DomRealm, Node, index, out value);
        }

        if (!_accessor.HasNamedGetter)
        {
            return false;
        }

        enumerable = _accessor.AreNamesEnumerable;
        return _accessor.TryGetNamed(DomRealm, Node, key, out value);
    }

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dfn-supported-property-indices - an index is ECMAScript's array
    /// index: a canonical numeric string below 2^32-1. Anything else is a <em>name</em>, which is what keeps
    /// <c>form['01']</c> and <c>form['-1']</c> out of the indexed half.
    /// </summary>
    private static bool IsArrayIndex(string key, out uint index)
    {
        index = 0;

        if (key.Length == 0 || key.Length > 10 || (key.Length > 1 && key[0] == '0'))
        {
            return false;
        }

        ulong value = 0;
        for (var i = 0; i < key.Length; i++)
        {
            var digit = key[i];
            if (digit < '0' || digit > '9')
            {
                return false;
            }

            value = (value * 10) + (uint) (digit - '0');
        }

        if (value >= uint.MaxValue)
        {
            return false;
        }

        index = (uint) value;
        return true;
    }
}
