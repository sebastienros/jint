using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Browser.SystemState;

/// <summary>The interfaces <see cref="SystemStateRealm"/> builds, which is also the index of its table.</summary>
internal enum SystemInterface
{
    Plugin,
    PluginArray,
    MimeType,
    MimeTypeArray,
    NavigatorUAData,
    Permissions,
    PermissionStatus,
    StorageManager,
    Notification,
    Screen,
    ScreenOrientation,
    VisualViewport,
    Gamepad,
}

/// <summary>
/// One of the document's singletons that is no <c>EventTarget</c>: <c>navigator.plugins</c>,
/// <c>navigator.mimeTypes</c>, <c>navigator.userAgentData</c>, <c>navigator.permissions</c>,
/// <c>navigator.storage</c> and <c>screen</c>.
/// </summary>
/// <remarks>
/// They carry no state beyond the page they answer about, so one class serves all six and the brand is its
/// <see cref="Kind"/>: <c>PluginArray.prototype.item.call(navigator.mimeTypes, 0)</c> is the
/// <c>TypeError</c> it is in a browser, not a quiet <see langword="null"/>.
/// </remarks>
internal sealed class JsSystemObject : ObjectInstance
{
    internal JsSystemObject(PageRuntime runtime, ObjectInstance prototype, SystemInterface kind)
        : base(runtime.Engine, ObjectClass.Object)
    {
        Runtime = runtime;
        Kind = kind;
        _prototype = prototype;
    }

    /// <summary>The page this object answers about.</summary>
    internal PageRuntime Runtime { get; }

    /// <summary>Which interface this object implements.</summary>
    internal SystemInterface Kind { get; }

    /// <summary>
    /// The hints a cached <c>FrozenArray</c> was built from, so that <c>navigator.userAgentData.brands</c> is
    /// the same array on every read until a client changes what it describes.
    /// </summary>
    internal object? CacheKey { get; set; }

    /// <summary>The cached value <see cref="CacheKey"/> keys.</summary>
    internal JsValue? CacheValue { get; set; }

    /// <summary>The receiver check every member of these interfaces starts with.</summary>
    internal static JsSystemObject Brand(JsValue thisObject, SystemInterface kind, string member)
    {
        if (thisObject is JsSystemObject instance && instance.Kind == kind)
        {
            return instance;
        }

        return SystemBrand.Refuse<JsSystemObject>(thisObject, SystemStateShapes.NameOf(kind), member);
    }

    /// <inheritdoc />
    public override string ToString() => "[object " + SystemStateShapes.NameOf(Kind) + "]";
}

/// <summary>The receiver checks of the system-state interfaces, in the words Chrome uses.</summary>
internal static class SystemBrand
{
    /// <summary>Answers the receiver when it is a <typeparamref name="T"/>, and refuses it otherwise.</summary>
    internal static T Of<T>(JsValue thisObject, string interfaceName, string member)
        where T : class
        => thisObject as T ?? Refuse<T>(thisObject, interfaceName, member);

    /// <summary>The <c>TypeError</c> a member called on the wrong receiver raises.</summary>
    internal static T Refuse<T>(JsValue thisObject, string interfaceName, string member)
    {
        var message = "Failed to execute '" + member + "' on '" + interfaceName + "': Illegal invocation";

        if (thisObject is ObjectInstance instance)
        {
            Throw.TypeError(instance.Engine.Realm, message);
        }

        Throw.TypeErrorNoEngine(message);
        return default!;
    }
}
