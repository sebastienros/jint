#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Locks;

/// <summary>
/// <c>Lock.prototype</c> — the interface prototype object, and where both members of a <c>Lock</c> live.
/// <para>
/// https://w3c.github.io/web-locks/#lock-class
/// </para>
/// </summary>
/// <remarks>
/// Both are WebIDL readonly attributes, so both are enumerable, configurable accessors here
/// (https://webidl.spec.whatwg.org/#es-attributes) and both brand-check their receiver: extracting the
/// getter and calling it on something else raises a <c>TypeError</c>, exactly as a browser does.
/// </remarks>
[JsObject(UseShape = true)]
internal sealed partial class LockPrototype : Prototype
{
    [JsProperty(Name = "constructor", Flags = PropertyFlag.NonEnumerable)]
    private readonly LockConstructor _constructor;

    /// <summary>
    /// https://webidl.spec.whatwg.org/#dfn-class-string — "the class string of an interface prototype object
    /// is the interface's qualified name".
    /// </summary>
    [JsSymbol("ToStringTag", Flags = PropertyFlag.Configurable)]
    private static readonly JsString LockToStringTag = new("Lock");

    internal LockPrototype(
        Engine engine,
        Realm realm,
        LockConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _prototype = objectPrototype;
        _constructor = constructor;
    }

    protected override void Initialize()
    {
        CreateProperties_Generated();
        CreateSymbols_Generated();
    }

    /// <summary>
    /// https://w3c.github.io/web-locks/#dom-lock-name — "the name getter's steps are to return the
    /// associated lock's name".
    /// </summary>
    [JsAccessor("name", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString NameGet(JsValue thisObject) => Brand(thisObject, "name").Name;

    /// <summary>
    /// https://w3c.github.io/web-locks/#dom-lock-mode — "the mode getter's steps are to return the
    /// associated lock's mode".
    /// </summary>
    [JsAccessor("mode", Flags = PropertyFlag.Configurable | PropertyFlag.Enumerable)]
    private JsString ModeGet(JsValue thisObject) => Brand(thisObject, "mode").Mode;

    private JsLock Brand(JsValue thisObject, string member)
    {
        if (thisObject is not JsLock instance)
        {
            Throw.TypeError(_realm, $"Failed to read the '{member}' property from 'Lock': illegal invocation, receiver is not a Lock object.");
            return null!;
        }

        return instance;
    }
}
#endif
