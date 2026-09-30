using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.WebApi.Events;
using Jint.WebApi.Streams;

namespace Jint.Browser.SystemState;

/// <summary>
/// The prototype shapes of the system-state interfaces, in the order each one's IDL declares its members.
/// </summary>
/// <remarks>
/// Every member starts with its receiver check, and every one is engine-independent — a shape is shared by
/// every engine, so a member finds its page through its receiver and never through a captured one.
/// </remarks>
internal static class SystemStateShapes
{
    /// <summary>One interface: its name, its prototype shape, whether it inherits <c>EventTarget</c>, its length.</summary>
    internal readonly record struct Definition(string Name, JsObjectShape Shape, bool EventTarget, int Length = 0);

    /// <summary>The table <see cref="SystemStateRealm"/> builds from, indexed by <see cref="SystemInterface"/>.</summary>
    internal static readonly Definition[] Definitions =
    [
        new("Plugin", BuildPluginShape(), EventTarget: false),
        new("PluginArray", BuildPluginArrayShape(), EventTarget: false),
        new("MimeType", BuildMimeTypeShape(), EventTarget: false),
        new("MimeTypeArray", BuildMimeTypeArrayShape(), EventTarget: false),
        new("NavigatorUAData", BuildUserAgentDataShape(), EventTarget: false),
        new("Permissions", BuildPermissionsShape(), EventTarget: false),
        new("PermissionStatus", BuildPermissionStatusShape(), EventTarget: true),
        new("StorageManager", BuildStorageManagerShape(), EventTarget: false),
        new("Notification", BuildNotificationShape(), EventTarget: true, Length: 1),
        new("Screen", BuildScreenShape(), EventTarget: false),
        new("ScreenOrientation", BuildScreenOrientationShape(), EventTarget: true),
        new("VisualViewport", BuildVisualViewportShape(), EventTarget: true),
    ];

    /// <summary>
    /// The quota <c>navigator.storage.estimate()</c> reports: a gibibyte, which is what LightPanda reports and
    /// comfortably above the thresholds a page uses to guess that it is in a private window.
    /// </summary>
    internal const double StorageQuota = 1024d * 1024 * 1024;

    /// <summary>https://notifications.spec.whatwg.org/#dom-notification-maxactions — Chrome's two.</summary>
    internal const int MaxNotificationActions = 2;

    internal static string NameOf(SystemInterface kind) => Definitions[(int) kind].Name;

    /// <summary>
    /// https://webidl.spec.whatwg.org/#es-operations — an operation returning a promise reports a failed
    /// receiver or argument conversion as a rejection, not a throw. A primitive receiver has no engine to
    /// build that promise in, so it still throws, which no page can tell apart from Chrome's rejection except
    /// by calling the method off an unrelated primitive.
    /// </summary>
    private static JsObjectShape.Builder PromiseMethod(
        this JsObjectShape.Builder builder,
        string name,
        Func<JsValue, JsValue[], JsValue> implementation,
        int length = 0)
        => builder.Method(name, (thisObject, arguments) =>
        {
            try
            {
                return implementation(thisObject, arguments);
            }
            catch (JavaScriptException exception) when (thisObject is ObjectInstance receiver)
            {
                return StreamPromises.RejectedWith(receiver.Engine, receiver.Engine._mainRealm, exception.Error);
            }
        }, length);

    private static JsSystemObject Of(JsValue thisObject, SystemInterface kind, string member)
        => JsSystemObject.Brand(thisObject, kind, member);

    /// <summary>
    /// A legacy platform object's <c>@@iterator</c>, which WebIDL makes <c>%Array.prototype.values%</c> for an
    /// interface with an indexed getter and a <c>length</c>.
    /// </summary>
    private static JsValue ArrayValues(ObjectInstance prototype)
        => prototype.Engine._mainRealm.Intrinsics.Array.PrototypeObject.Get("values");

    private static JsValue RequireArgument(JsValue thisObject, JsValue[] arguments, string interfaceName, string member)
    {
        if (arguments.Length == 0)
        {
            var message = "Failed to execute '" + member + "' on '" + interfaceName + "': 1 argument required, but only 0 present.";
            if (thisObject is ObjectInstance instance)
            {
                Throw.TypeError(instance.Engine.Realm, message);
            }

            Throw.TypeErrorNoEngine(message);
        }

        return arguments[0];
    }

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/system-state.html#plugin — no instance ever exists, so every
    /// member refuses whatever it is called on.
    /// </summary>
    private static JsObjectShape BuildPluginShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("Plugin")
        .Accessor("name", static (t, _) => Of(t, SystemInterface.Plugin, "name"))
        .Accessor("description", static (t, _) => Of(t, SystemInterface.Plugin, "description"))
        .Accessor("filename", static (t, _) => Of(t, SystemInterface.Plugin, "filename"))
        .Accessor("length", static (t, _) => Of(t, SystemInterface.Plugin, "length"))
        .Method("item", static (t, _) => Of(t, SystemInterface.Plugin, "item"), length: 1)
        .Method("namedItem", static (t, _) => Of(t, SystemInterface.Plugin, "namedItem"), length: 1)
        .PerRealmSlot(GlobalSymbolRegistry.Iterator, ArrayValues)
        .Build();

    /// <summary>
    /// https://html.spec.whatwg.org/multipage/system-state.html#pluginarray — empty, because
    /// <c>pdfViewerEnabled</c> is false and HTML then says there are no plugins to list.
    /// </summary>
    private static JsObjectShape BuildPluginArrayShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("PluginArray")
        .Method("refresh", static (t, _) =>
        {
            Of(t, SystemInterface.PluginArray, "refresh");
            return JsValue.Undefined;
        })
        .Accessor("length", static (t, _) =>
        {
            Of(t, SystemInterface.PluginArray, "length");
            return JsNumber.PositiveZero;
        })
        .Method("item", static (t, args) => EmptyItem(t, args, SystemInterface.PluginArray, "item", index: true), length: 1)
        .Method("namedItem", static (t, args) => EmptyItem(t, args, SystemInterface.PluginArray, "namedItem", index: false), length: 1)
        .PerRealmSlot(GlobalSymbolRegistry.Iterator, ArrayValues)
        .Build();

    /// <summary>https://html.spec.whatwg.org/multipage/system-state.html#mimetype — like <c>Plugin</c>, never instantiated.</summary>
    private static JsObjectShape BuildMimeTypeShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("MimeType")
        .Accessor("type", static (t, _) => Of(t, SystemInterface.MimeType, "type"))
        .Accessor("description", static (t, _) => Of(t, SystemInterface.MimeType, "description"))
        .Accessor("suffixes", static (t, _) => Of(t, SystemInterface.MimeType, "suffixes"))
        .Accessor("enabledPlugin", static (t, _) => Of(t, SystemInterface.MimeType, "enabledPlugin"))
        .Build();

    /// <summary>https://html.spec.whatwg.org/multipage/system-state.html#mimetypearray</summary>
    private static JsObjectShape BuildMimeTypeArrayShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("MimeTypeArray")
        .Accessor("length", static (t, _) =>
        {
            Of(t, SystemInterface.MimeTypeArray, "length");
            return JsNumber.PositiveZero;
        })
        .Method("item", static (t, args) => EmptyItem(t, args, SystemInterface.MimeTypeArray, "item", index: true), length: 1)
        .Method("namedItem", static (t, args) => EmptyItem(t, args, SystemInterface.MimeTypeArray, "namedItem", index: false), length: 1)
        .PerRealmSlot(GlobalSymbolRegistry.Iterator, ArrayValues)
        .Build();

    /// <summary>
    /// <c>item</c> and <c>namedItem</c> of an empty collection: the argument is still required and still
    /// converted, and the answer is always <see langword="null"/>.
    /// </summary>
    private static JsValue EmptyItem(JsValue thisObject, JsValue[] arguments, SystemInterface kind, string member, bool index)
    {
        Of(thisObject, kind, member);
        var argument = RequireArgument(thisObject, arguments, NameOf(kind), member);
        _ = index ? TypeConverter.ToUint32(argument) : (object) TypeConverter.ToString(argument);
        return JsValue.Null;
    }

    /// <summary>https://wicg.github.io/ua-client-hints/#navigatoruadata</summary>
    private static JsObjectShape BuildUserAgentDataShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("NavigatorUAData")
        .Accessor("brands", static (t, _) => NavigatorUserAgentData.Brands(Of(t, SystemInterface.NavigatorUAData, "brands")))
        .Accessor("mobile", static (t, _) => NavigatorUserAgentData.Mobile(Of(t, SystemInterface.NavigatorUAData, "mobile")))
        .Accessor("platform", static (t, _) => NavigatorUserAgentData.Platform(Of(t, SystemInterface.NavigatorUAData, "platform")))
        .PromiseMethod("getHighEntropyValues", static (t, args) => NavigatorUserAgentData.GetHighEntropyValues(
            Of(t, SystemInterface.NavigatorUAData, "getHighEntropyValues"),
            RequireArgument(t, args, "NavigatorUAData", "getHighEntropyValues")), length: 1)
        .Method("toJSON", static (t, _) => NavigatorUserAgentData.ToJson(Of(t, SystemInterface.NavigatorUAData, "toJSON")))
        .Build();

    /// <summary>https://w3c.github.io/permissions/#permissions-interface</summary>
    private static JsObjectShape BuildPermissionsShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("Permissions")
        .PromiseMethod("query", static (t, args) => NavigatorPermissions.Query(
            Of(t, SystemInterface.Permissions, "query"),
            RequireArgument(t, args, "Permissions", "query")), length: 1)
        .Build();

    /// <summary>https://w3c.github.io/permissions/#permissionstatus-interface</summary>
    private static JsObjectShape BuildPermissionStatusShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("PermissionStatus")
        .Accessor("state", static (t, _) => JsString.Create(SystemBrand.Of<JsPermissionStatus>(t, "PermissionStatus", "state").State))
        .Accessor("name", static (t, _) => JsString.Create(SystemBrand.Of<JsPermissionStatus>(t, "PermissionStatus", "name").Name))
        .Accessor("onchange",
            static (t, _) => EventHandlerAttributes.Get(SystemBrand.Of<JsPermissionStatus>(t, "PermissionStatus", "onchange"), "change"),
            static (t, args) => EventHandlerAttributes.Set(SystemBrand.Of<JsPermissionStatus>(t, "PermissionStatus", "onchange"), "change", args.At(0)))
        .Build();

    /// <summary>
    /// https://storage.spec.whatwg.org/#storagemanager — nothing a page stores here is quota-managed and
    /// nothing is ever persisted, so the usage is zero and both persistence questions answer false.
    /// </summary>
    private static JsObjectShape BuildStorageManagerShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("StorageManager")
        .PromiseMethod("persisted", static (t, _) => ResolvedWith(Of(t, SystemInterface.StorageManager, "persisted"), JsBoolean.False))
        .PromiseMethod("persist", static (t, _) => ResolvedWith(Of(t, SystemInterface.StorageManager, "persist"), JsBoolean.False))
        .PromiseMethod("estimate", static (t, _) =>
        {
            var manager = Of(t, SystemInterface.StorageManager, "estimate");

            // StorageEstimate is a dictionary, so its members arrive in lexicographic order.
            var estimate = new JsObject(manager.Engine);
            estimate.CreateDataPropertyOrThrow("quota", StorageQuota);
            estimate.CreateDataPropertyOrThrow("usage", 0);
            return ResolvedWith(manager, estimate);
        })
        .Build();

    private static JsPromise ResolvedWith(JsSystemObject receiver, JsValue value)
        => StreamPromises.ResolvedWith(receiver.Engine, receiver.Engine._mainRealm, value);

    /// <summary>https://notifications.spec.whatwg.org/#api</summary>
    private static JsObjectShape BuildNotificationShape()
    {
        var builder = new JsObjectShape.Builder()
            .PerRealmSlot("constructor")
            .ToStringTag("Notification");

        foreach (var type in (string[]) ["click", "show", "error", "close"])
        {
            var name = "on" + type;
            builder.Accessor(
                name,
                (t, _) => EventHandlerAttributes.Get(Notification(t, name), type),
                (t, args) => EventHandlerAttributes.Set(Notification(t, name), type, args.At(0)));
        }

        return builder
            .Accessor("title", static (t, _) => JsString.Create(Notification(t, "title").Title))
            .Accessor("dir", static (t, _) => JsString.Create(Notification(t, "dir").Dir))
            .Accessor("lang", static (t, _) => JsString.Create(Notification(t, "lang").Lang))
            .Accessor("body", static (t, _) => JsString.Create(Notification(t, "body").Body))
            .Accessor("tag", static (t, _) => JsString.Create(Notification(t, "tag").Tag))
            .Accessor("image", static (t, _) => JsString.Create(Notification(t, "image").Image))
            .Accessor("icon", static (t, _) => JsString.Create(Notification(t, "icon").Icon))
            .Accessor("badge", static (t, _) => JsString.Create(Notification(t, "badge").Badge))
            .Accessor("vibrate", static (t, _) => Notification(t, "vibrate").Vibrate)
            .Accessor("timestamp", static (t, _) => JsNumber.Create(Notification(t, "timestamp").Timestamp))
            .Accessor("renotify", static (t, _) => Notification(t, "renotify").Renotify ? JsBoolean.True : JsBoolean.False)
            .Accessor("silent", static (t, _) => Notification(t, "silent").Silent is { } silent ? (silent ? JsBoolean.True : JsBoolean.False) : JsValue.Null)
            .Accessor("requireInteraction", static (t, _) => Notification(t, "requireInteraction").RequireInteraction ? JsBoolean.True : JsBoolean.False)
            .Accessor("data", static (t, _) => Notification(t, "data").Data)
            .Accessor("actions", static (t, _) => Notification(t, "actions").Actions)
            .Method("close", static (t, _) =>
            {
                // Never shown, so there is nothing to close and no `close` event to fire.
                Notification(t, "close");
                return JsValue.Undefined;
            })
            .Build();
    }

    private static JsNotification Notification(JsValue thisObject, string member)
        => SystemBrand.Of<JsNotification>(thisObject, "Notification", member);

    /// <summary>
    /// https://drafts.csswg.org/cssom-view/#the-screen-interface, with Screen Orientation's
    /// <c>orientation</c> and the three members Chrome adds for multi-screen windows, all describing one
    /// screen exactly the size of the viewport.
    /// </summary>
    private static JsObjectShape BuildScreenShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("Screen")
        .Accessor("availWidth", static (t, _) => JsNumber.Create(Of(t, SystemInterface.Screen, "availWidth").Runtime.Viewport.Width))
        .Accessor("availHeight", static (t, _) => JsNumber.Create(Of(t, SystemInterface.Screen, "availHeight").Runtime.Viewport.Height))
        .Accessor("width", static (t, _) => JsNumber.Create(Of(t, SystemInterface.Screen, "width").Runtime.Viewport.Width))
        .Accessor("height", static (t, _) => JsNumber.Create(Of(t, SystemInterface.Screen, "height").Runtime.Viewport.Height))
        .Accessor("colorDepth", static (t, _) =>
        {
            Of(t, SystemInterface.Screen, "colorDepth");
            return JsNumber.Create(24);
        })
        .Accessor("pixelDepth", static (t, _) =>
        {
            Of(t, SystemInterface.Screen, "pixelDepth");
            return JsNumber.Create(24);
        })
        .Accessor("availLeft", static (t, _) =>
        {
            Of(t, SystemInterface.Screen, "availLeft");
            return JsNumber.PositiveZero;
        })
        .Accessor("availTop", static (t, _) =>
        {
            Of(t, SystemInterface.Screen, "availTop");
            return JsNumber.PositiveZero;
        })
        .Accessor("isExtended", static (t, _) =>
        {
            Of(t, SystemInterface.Screen, "isExtended");
            return JsBoolean.False;
        })
        .Accessor("orientation", static (t, _) => Of(t, SystemInterface.Screen, "orientation").Runtime.SystemState.Orientation)
        .Build();

    /// <summary>https://w3c.github.io/screen-orientation/#screenorientation-interface</summary>
    private static JsObjectShape BuildScreenOrientationShape() => new JsObjectShape.Builder()
        .PerRealmSlot("constructor")
        .ToStringTag("ScreenOrientation")
        .PromiseMethod("lock", static (t, args) =>
        {
            var orientation = SystemBrand.Of<JsScreenOrientation>(t, "ScreenOrientation", "lock");
            var engine = orientation.Engine;
            var realm = engine._mainRealm;

            // The OrientationLockType enum first, as WebIDL converts the argument before the steps run.
            var type = TypeConverter.ToString(RequireArgument(t, args, "ScreenOrientation", "lock"));
            if (type is not ("any" or "natural" or "landscape" or "portrait" or "portrait-primary"
                or "portrait-secondary" or "landscape-primary" or "landscape-secondary"))
            {
                return StreamPromises.RejectedWith(engine, realm, realm.Intrinsics.TypeError.Construct(
                    "Failed to execute 'lock' on 'ScreenOrientation': The provided value '" + type
                    + "' is not a valid enum value of type OrientationLockType."));
            }

            // https://w3c.github.io/screen-orientation/#dfn-apply-orientation-lock — a desktop screen cannot
            // be turned, and the standard's answer for a user agent that cannot lock is NotSupportedError.
            return StreamPromises.RejectedWith(engine, realm, realm.Intrinsics.DomException.CreateException(
                Jint.WebApi.DomException.DomExceptionNames.NotSupported,
                "screen.orientation.lock() is not available on this device."));
        }, length: 1)
        .Method("unlock", static (t, _) =>
        {
            SystemBrand.Of<JsScreenOrientation>(t, "ScreenOrientation", "unlock");
            return JsValue.Undefined;
        })
        .Accessor("type", static (t, _) => JsString.Create(SystemBrand.Of<JsScreenOrientation>(t, "ScreenOrientation", "type").OrientationType))
        .Accessor("angle", static (t, _) =>
        {
            SystemBrand.Of<JsScreenOrientation>(t, "ScreenOrientation", "angle");
            return JsNumber.PositiveZero;
        })
        .Accessor("onchange",
            static (t, _) => EventHandlerAttributes.Get(SystemBrand.Of<JsScreenOrientation>(t, "ScreenOrientation", "onchange"), "change"),
            static (t, args) => EventHandlerAttributes.Set(SystemBrand.Of<JsScreenOrientation>(t, "ScreenOrientation", "onchange"), "change", args.At(0)))
        .Build();

    /// <summary>https://drafts.csswg.org/cssom-view/#the-visualviewport-interface</summary>
    private static JsObjectShape BuildVisualViewportShape()
    {
        var builder = new JsObjectShape.Builder()
            .PerRealmSlot("constructor")
            .ToStringTag("VisualViewport")
            .Accessor("offsetLeft", static (t, _) => Zero(t, "offsetLeft"))
            .Accessor("offsetTop", static (t, _) => Zero(t, "offsetTop"))
            .Accessor("pageLeft", static (t, _) => Zero(t, "pageLeft"))
            .Accessor("pageTop", static (t, _) => JsNumber.Create(Viewport(t, "pageTop").Runtime.Layout.ScrollY))
            .Accessor("width", static (t, _) => JsNumber.Create(Viewport(t, "width").Runtime.Viewport.Width))
            .Accessor("height", static (t, _) => JsNumber.Create(Viewport(t, "height").Runtime.Viewport.Height))
            .Accessor("scale", static (t, _) =>
            {
                Viewport(t, "scale");
                return JsNumber.PositiveOne;
            });

        foreach (var type in (string[]) ["resize", "scroll", "scrollend"])
        {
            var name = "on" + type;
            builder.Accessor(
                name,
                (t, _) => EventHandlerAttributes.Get(Viewport(t, name), type),
                (t, args) => EventHandlerAttributes.Set(Viewport(t, name), type, args.At(0)));
        }

        return builder.Build();
    }

    private static JsVisualViewport Viewport(JsValue thisObject, string member)
        => SystemBrand.Of<JsVisualViewport>(thisObject, "VisualViewport", member);

    private static JsNumber Zero(JsValue thisObject, string member)
    {
        Viewport(thisObject, member);
        return JsNumber.PositiveZero;
    }
}
