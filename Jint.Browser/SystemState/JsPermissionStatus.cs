using Jint.Browser.Runtime;
using Jint.Native.Object;
using Jint.WebApi.Events;

namespace Jint.Browser.SystemState;

/// <summary>
/// What <c>navigator.permissions.query</c> resolves with: a permission's name and the state it is in.
/// </summary>
/// <remarks>
/// https://w3c.github.io/permissions/#permissionstatus-interface — an <c>EventTarget</c> so that
/// <c>onchange</c> exists for the page that assigns it. Nothing here moves a permission's state after a
/// document has asked, so the event it names never fires; a new query is a new object, as the standard says.
/// </remarks>
internal sealed class JsPermissionStatus : JsEventTarget
{
    internal JsPermissionStatus(PageRuntime runtime, ObjectInstance prototype, string name, string state)
        : base(runtime.Engine, runtime.Engine._mainRealm)
    {
        Name = name;
        State = state;
        _prototype = prototype;
    }

    /// <summary>https://w3c.github.io/permissions/#dom-permissionstatus-name</summary>
    internal string Name { get; }

    /// <summary>https://w3c.github.io/permissions/#dom-permissionstatus-state</summary>
    internal string State { get; }

    /// <inheritdoc />
    public override string ToString() => "[object PermissionStatus]";
}
