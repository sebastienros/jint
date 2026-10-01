using Jint.Browser.Dom;
using Jint.Browser.Dom.Views;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;
using Jint.WebApi.Streams;

namespace Jint.Browser.SystemState;

/// <summary>
/// The system-state interfaces of one page: <c>navigator</c>'s plugin, client-hint, permission and storage
/// objects, <c>Notification</c>, <c>Screen</c>, <c>ScreenOrientation</c> and <c>VisualViewport</c> — each
/// prototype and interface object built on first use, and each singleton the page reaches them through.
/// </summary>
/// <remarks>
/// <para>
/// The name is HTML's section title for most of them, "System state and capabilities"; the screen and the
/// visual viewport are CSSOM View's, and are here because they answer the same question — what is the device
/// this page runs on — from the same emulation state.
/// </para>
/// <para>
/// Like <see cref="ViewRealm"/>, nothing is built until a page names it: a page that never reads
/// <c>navigator.permissions</c> never builds <c>Permissions.prototype</c>.
/// </para>
/// </remarks>
internal sealed class SystemStateRealm
{
    private readonly PageRuntime _runtime;
    private readonly (ObjectInstance Prototype, HostInterfaceObject Interface)?[] _built =
        new (ObjectInstance, HostInterfaceObject)?[SystemStateShapes.Definitions.Length];

    private JsSystemObject? _plugins;
    private JsSystemObject? _mimeTypes;
    private JsSystemObject? _userAgentData;
    private JsSystemObject? _permissions;
    private JsSystemObject? _storage;
    private JsSystemObject? _screen;
    private JsScreenOrientation? _orientation;
    private JsVisualViewport? _visualViewport;

    internal SystemStateRealm(PageRuntime runtime)
    {
        _runtime = runtime;
    }

    /// <summary>Installs every interface object as a lazy, non-enumerable global.</summary>
    internal static void Install(PageRuntime runtime)
    {
        var engine = runtime.Engine;

        for (var i = 0; i < SystemStateShapes.Definitions.Length; i++)
        {
            engine.AddLazyGlobal(
                SystemStateShapes.Definitions[i].Name,
                (SystemInterface) i,
                static (e, kind) => PageRuntime.Find(e)!.SystemState.Interface(kind),
                PropertyFlag.NonEnumerable);
        }
    }

    /// <summary>The prototype of <paramref name="kind"/>, built with its interface on first use.</summary>
    internal ObjectInstance Prototype(SystemInterface kind) => Built(kind).Prototype;

    /// <summary>The interface object of <paramref name="kind"/>, built on first use.</summary>
    internal HostInterfaceObject Interface(SystemInterface kind) => Built(kind).Interface;

    /// <summary>https://html.spec.whatwg.org/multipage/system-state.html#dom-navigator-plugins</summary>
    internal JsSystemObject Plugins => _plugins ??= Singleton(SystemInterface.PluginArray);

    /// <summary>https://html.spec.whatwg.org/multipage/system-state.html#dom-navigator-mimetypes</summary>
    internal JsSystemObject MimeTypes => _mimeTypes ??= Singleton(SystemInterface.MimeTypeArray);

    /// <summary>https://wicg.github.io/ua-client-hints/#dom-navigatorua-useragentdata</summary>
    internal JsSystemObject UserAgentData => _userAgentData ??= Singleton(SystemInterface.NavigatorUAData);

    /// <summary>https://w3c.github.io/permissions/#dom-navigator-permissions</summary>
    internal JsSystemObject Permissions => _permissions ??= Singleton(SystemInterface.Permissions);

    /// <summary>https://storage.spec.whatwg.org/#dom-navigatorstorage-storage</summary>
    internal JsSystemObject Storage => _storage ??= Singleton(SystemInterface.StorageManager);

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-window-screen</summary>
    internal JsSystemObject Screen => _screen ??= Singleton(SystemInterface.Screen);

    /// <summary>https://w3c.github.io/screen-orientation/#dom-screen-orientation</summary>
    internal JsScreenOrientation Orientation
        => _orientation ??= new JsScreenOrientation(_runtime, Prototype(SystemInterface.ScreenOrientation));

    /// <summary>https://drafts.csswg.org/cssom-view/#dom-window-visualviewport</summary>
    internal JsVisualViewport VisualViewport
        => _visualViewport ??= new JsVisualViewport(_runtime, Prototype(SystemInterface.VisualViewport));

    /// <summary>
    /// Tells the screen's orientation and the visual viewport that the viewport changed, so each fires the
    /// event its own answer moving calls for. Neither is built for the purpose.
    /// </summary>
    internal void ViewportChanged()
    {
        _orientation?.ViewportChanged();
        _visualViewport?.ViewportChanged();
    }

    private JsSystemObject Singleton(SystemInterface kind) => new(_runtime, Prototype(kind), kind);

    private (ObjectInstance Prototype, HostInterfaceObject Interface) Built(SystemInterface kind)
    {
        if (_built[(int) kind] is { } built)
        {
            return built;
        }

        var engine = _runtime.Engine;
        var realm = engine._mainRealm;
        var definition = SystemStateShapes.Definitions[(int) kind];

        Func<JsValue[], ObjectInstance>? construct = kind == SystemInterface.Notification
            ? args => JsNotification.Construct(_runtime, Prototype(SystemInterface.Notification), args)
            : null;

        var prototype = ViewInstaller.Instantiate(
            engine,
            definition.Shape,
            definition.Name,
            definition.Length,
            construct,
            definition.EventTarget ? realm.Intrinsics.EventTarget.PrototypeObject : null,
            definition.EventTarget ? realm.Intrinsics.EventTarget : null,
            out var interfaceObject);

        if (kind == SystemInterface.Notification)
        {
            AddNotificationStatics(interfaceObject);
        }

        var result = (prototype, interfaceObject);
        _built[(int) kind] = result;
        return result;
    }

    /// <summary>
    /// https://notifications.spec.whatwg.org/#api — <c>permission</c>, <c>requestPermission</c> and
    /// <c>maxActions</c>, the interface's three static members.
    /// </summary>
    private void AddNotificationStatics(HostInterfaceObject notification)
    {
        var engine = _runtime.Engine;
        var runtime = _runtime;

        notification.DefineOwnPropertyUnchecked(
            "permission",
            new GetSetPropertyDescriptor(
                new ClrFunction(engine, "get permission", (_, _) => JsString.Create(JsNotification.NotificationPermission(runtime))),
                set: null,
                PropertyFlag.Configurable | PropertyFlag.Enumerable));

        notification.DefineOwnPropertyUnchecked(
            "requestPermission",
            new PropertyDescriptor(
                new ClrFunction(engine, "requestPermission", (_, args) => RequestPermission(runtime, args.At(0)), 0, PropertyFlag.Configurable),
                PropertyFlag.ConfigurableEnumerableWritable));

        notification.DefineOwnPropertyUnchecked(
            "maxActions",
            new GetSetPropertyDescriptor(
                new ClrFunction(engine, "get maxActions", (_, _) => JsNumber.Create(SystemStateShapes.MaxNotificationActions)),
                set: null,
                PropertyFlag.Configurable | PropertyFlag.Enumerable));
    }

    /// <summary>
    /// https://notifications.spec.whatwg.org/#dom-notification-requestpermission — nobody is there to answer
    /// the prompt, so the answer is the one already standing, delivered to the legacy callback and the promise
    /// alike from a task.
    /// </summary>
    private static JsValue RequestPermission(PageRuntime runtime, JsValue callback)
    {
        var engine = runtime.Engine;
        var realm = engine._mainRealm;

        if (!callback.IsUndefined() && callback is not ICallable)
        {
            return StreamPromises.RejectedWith(engine, realm, realm.Intrinsics.TypeError.Construct(
                "Failed to execute 'requestPermission' on 'Notification': The callback provided as parameter 1 is not a function."));
        }

        var (promise, resolve, _) = engine.RegisterPromise();
        engine.AddToEventLoop(
            () =>
            {
                var permission = JsString.Create(JsNotification.NotificationPermission(runtime));
                if (callback is ICallable)
                {
                    engine.Call(callback, JsValue.Undefined, [permission]);
                }

                resolve(permission);
            },
            EventLoopJobKind.Task);

        return promise;
    }
}
