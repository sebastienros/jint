using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Events;
using Jint.WebApi.StructuredClone;

namespace Jint.Browser.SystemState;

/// <summary>
/// A <c>new Notification(title, options)</c>: everything the page described, kept and read back, and the
/// <c>error</c> event a notification nobody gave permission for fires.
/// </summary>
/// <remarks>
/// <para>
/// https://notifications.spec.whatwg.org/#notifications — the constructor validates and records exactly as
/// the standard's "create a notification" does, so a page reading <c>n.body</c> or <c>n.timestamp</c> back gets
/// what a browser would give it. The show steps run too, and they find the permission not granted:
/// <see cref="NotificationPermission"/> answers <c>"default"</c>, so the notification is never shown and
/// <c>error</c> fires at it as a task, which is what a browser whose user never answered the prompt does.
/// </para>
/// <para>
/// <b><c>data</c> is serialized in the constructor and deserialized on every read</b>, as the standard says —
/// so a function in it is a <c>DataCloneError</c> at <c>new Notification</c>, and <c>n.data !== n.data</c>.
/// </para>
/// </remarks>
internal sealed class JsNotification : JsEventTarget
{
    private static readonly string[] _directions = ["auto", "ltr", "rtl"];

    private readonly PageRuntime _runtime;
    private readonly SerializationRecord _data;
    private readonly uint[] _vibrate;
    private JsValue? _vibrateArray;
    private JsValue? _actionsArray;

    private JsNotification(PageRuntime runtime, ObjectInstance prototype, NotificationOptions options, SerializationRecord data)
        : base(runtime.Engine, runtime.Engine._mainRealm)
    {
        _runtime = runtime;
        _prototype = prototype;
        _data = data;
        _vibrate = options.Vibrate;
        Title = options.Title;
        Dir = options.Dir;
        Lang = options.Lang;
        Body = options.Body;
        Tag = options.Tag;
        Image = options.Image;
        Icon = options.Icon;
        Badge = options.Badge;
        Timestamp = options.Timestamp;
        Renotify = options.Renotify;
        Silent = options.Silent;
        RequireInteraction = options.RequireInteraction;
    }

    internal string Title { get; }

    internal string Dir { get; }

    internal string Lang { get; }

    internal string Body { get; }

    internal string Tag { get; }

    internal string Image { get; }

    internal string Icon { get; }

    internal string Badge { get; }

    internal double Timestamp { get; }

    internal bool Renotify { get; }

    internal bool? Silent { get; }

    internal bool RequireInteraction { get; }

    /// <summary>https://notifications.spec.whatwg.org/#dom-notification-notification</summary>
    internal static JsNotification Construct(PageRuntime runtime, ObjectInstance prototype, JsValue[] arguments)
    {
        var engine = runtime.Engine;
        var realm = engine._mainRealm;

        if (arguments.Length == 0)
        {
            Throw.TypeError(realm, "Failed to construct 'Notification': 1 argument required, but only 0 present.");
        }

        var options = ReadOptions(runtime, TypeConverter.ToString(arguments[0]), arguments.At(1));

        // Step 2: a non-persistent notification has no actions to offer.
        if (options.ActionCount > 0)
        {
            Throw.TypeError(
                realm,
                "Failed to construct 'Notification': Actions are only supported for persistent notifications shown using ServiceWorkerRegistration.showNotification().");
        }

        // https://notifications.spec.whatwg.org/#create-a-notification steps 2 and 3.
        if (options.Silent == true && options.HasVibrate)
        {
            Throw.TypeError(realm, "Failed to construct 'Notification': Silent notifications must not specify vibration patterns.");
        }

        if (options.Renotify && options.Tag.Length == 0)
        {
            Throw.TypeError(realm, "Failed to construct 'Notification': Notifications which set the renotify flag must specify a non-empty tag.");
        }

        var data = new StructuredSerializer(engine, realm).Serialize(options.Data, transferList: null);
        var notification = new JsNotification(runtime, prototype, options, data);

        // The show steps: the permission is not "granted", so queue a task to fire `error` at it.
        engine.AddToEventLoop(() => PageEvents.Fire(runtime, notification, "error"), EventLoopJobKind.Task);
        return notification;
    }

    /// <summary>https://notifications.spec.whatwg.org/#dom-notification-data</summary>
    internal JsValue Data => new StructuredDeserializer(_runtime.Engine, _runtime.Engine._mainRealm).Deserialize(_data);

    /// <summary>https://notifications.spec.whatwg.org/#dom-notification-vibrate, one frozen array for life.</summary>
    internal JsValue Vibrate => _vibrateArray ??= FrozenArray([.. _vibrate.Select(static v => (JsValue) JsNumber.Create(v))]);

    /// <summary>https://notifications.spec.whatwg.org/#dom-notification-actions — always empty here.</summary>
    internal JsValue Actions => _actionsArray ??= FrozenArray([]);

    /// <summary>
    /// https://notifications.spec.whatwg.org/#dom-notification-permission — the notification permission
    /// state, in the words <c>Notification.permission</c> uses for it.
    /// </summary>
    internal static string NotificationPermission(PageRuntime runtime)
        => NavigatorPermissions.StateOf(runtime, "notifications") switch
        {
            "granted" => "granted",
            "denied" => "denied",
            _ => "default",
        };

    /// <inheritdoc />
    public override string ToString() => "[object Notification]";

    private JsArray FrozenArray(JsValue[] values)
    {
        var array = _runtime.Engine._mainRealm.Intrinsics.Array.ConstructFast(values);
        array.SetIntegrityLevel(IntegrityLevel.Frozen);
        return array;
    }

    /// <summary>
    /// https://notifications.spec.whatwg.org/#dictdef-notificationoptions — every member read in
    /// lexicographic order, as WebIDL converts a dictionary, so a page's getters observe the order a browser's
    /// do.
    /// </summary>
    private static NotificationOptions ReadOptions(PageRuntime runtime, string title, JsValue value)
    {
        var realm = runtime.Engine._mainRealm;
        var options = new NotificationOptions { Title = title, Timestamp = runtime.Engine.Options.TimeSystem.GetUtcNow().ToUnixTimeMilliseconds() };

        if (value.IsNullOrUndefined())
        {
            return options;
        }

        if (value is not ObjectInstance dictionary)
        {
            Throw.TypeError(realm, "Failed to construct 'Notification': The provided value is not of type 'NotificationOptions'.");
            return options;
        }

        if (Member(dictionary, "actions") is { } actions)
        {
            options.ActionCount = Count(realm, actions);
        }

        if (Member(dictionary, "badge") is { } badge)
        {
            options.Badge = Url(runtime, badge);
        }

        if (Member(dictionary, "body") is { } body)
        {
            options.Body = TypeConverter.ToString(body);
        }

        options.Data = Member(dictionary, "data") ?? JsValue.Null;

        if (Member(dictionary, "dir") is { } dir)
        {
            var direction = TypeConverter.ToString(dir);
            if (Array.IndexOf(_directions, direction) < 0)
            {
                Throw.TypeError(
                    realm,
                    "Failed to construct 'Notification': Failed to read the 'dir' property from 'NotificationOptions': The provided value '"
                    + direction + "' is not a valid enum value of type NotificationDirection.");
            }

            options.Dir = direction;
        }

        if (Member(dictionary, "icon") is { } icon)
        {
            options.Icon = Url(runtime, icon);
        }

        if (Member(dictionary, "image") is { } image)
        {
            options.Image = Url(runtime, image);
        }

        if (Member(dictionary, "lang") is { } lang)
        {
            options.Lang = TypeConverter.ToString(lang);
        }

        if (Member(dictionary, "renotify") is { } renotify)
        {
            options.Renotify = TypeConverter.ToBoolean(renotify);
        }

        if (Member(dictionary, "requireInteraction") is { } requireInteraction)
        {
            options.RequireInteraction = TypeConverter.ToBoolean(requireInteraction);
        }

        if (Member(dictionary, "silent") is { } silent)
        {
            options.Silent = silent.IsNull() ? null : TypeConverter.ToBoolean(silent);
        }

        if (Member(dictionary, "tag") is { } tag)
        {
            options.Tag = TypeConverter.ToString(tag);
        }

        if (Member(dictionary, "timestamp") is { } timestamp)
        {
            // EpochTimeStamp is an unsigned long long, which WebIDL reaches by ToNumber and truncation.
            var number = TypeConverter.ToNumber(timestamp);
            options.Timestamp = double.IsNaN(number) || double.IsInfinity(number) ? 0 : Math.Abs(Math.Truncate(number));
        }

        if (Member(dictionary, "vibrate") is { } vibrate)
        {
            options.HasVibrate = true;
            options.Vibrate = VibratePattern(realm, vibrate);
        }

        return options;
    }

    /// <summary>A dictionary member that is present, which WebIDL says is one that is not undefined.</summary>
    private static JsValue? Member(ObjectInstance dictionary, string name)
    {
        var value = dictionary.Get(name);
        return value.IsUndefined() ? null : value;
    }

    /// <summary>https://notifications.spec.whatwg.org/#create-a-notification — a URL that fails to parse is none.</summary>
    private static string Url(PageRuntime runtime, JsValue value)
        => PageUrl.Resolve(TypeConverter.ToString(value), runtime.BaseUri) ?? "";

    /// <summary>
    /// https://w3c.github.io/vibration/#dfn-validate-and-normalize — <c>(unsigned long or
    /// sequence&lt;unsigned long&gt;)</c>, where a single number is a one-entry pattern.
    /// </summary>
    private static uint[] VibratePattern(Realm realm, JsValue value)
    {
        if (value is ObjectInstance instance && instance.GetMethod(Native.Symbol.GlobalSymbolRegistry.Iterator) is not null)
        {
            var iterator = value.GetIterator(realm);
            var pattern = new List<uint>();
            while (iterator.TryIteratorStepValue(out var item))
            {
                pattern.Add(TypeConverter.ToUint32(item));
            }

            return [.. pattern];
        }

        return [TypeConverter.ToUint32(value)];
    }

    /// <summary>How many entries an <c>actions</c> sequence has, which is all the constructor asks of it.</summary>
    private static int Count(Realm realm, JsValue value)
    {
        if (value is not ObjectInstance instance || instance.GetMethod(Native.Symbol.GlobalSymbolRegistry.Iterator) is null)
        {
            Throw.TypeError(
                realm,
                "Failed to construct 'Notification': Failed to read the 'actions' property from 'NotificationOptions': The object must have a callable @@iterator property.");
            return 0;
        }

        var iterator = value.GetIterator(realm);
        var count = 0;
        while (iterator.TryIteratorStepValue(out _))
        {
            count++;
        }

        return count;
    }

    private sealed class NotificationOptions
    {
        internal string Title = "";
        internal int ActionCount;
        internal string Badge = "";
        internal string Body = "";
        internal JsValue Data = JsValue.Null;
        internal string Dir = "auto";
        internal string Icon = "";
        internal string Image = "";
        internal string Lang = "";
        internal bool Renotify;
        internal bool RequireInteraction;
        internal bool? Silent;
        internal string Tag = "";
        internal double Timestamp;
        internal bool HasVibrate;
        internal uint[] Vibrate = [];
    }
}
