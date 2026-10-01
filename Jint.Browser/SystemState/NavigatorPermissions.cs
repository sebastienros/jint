using System.Collections.Frozen;
using Jint.Browser.Runtime;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.Streams;

namespace Jint.Browser.SystemState;

/// <summary>
/// <c>navigator.permissions</c>: what a page is told when it asks whether it may use a powerful feature.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every permission is <c>"prompt"</c></b>: nobody has granted anything and nobody has refused anything,
/// which is the truth of a browser with no user to ask. <c>Notification.permission</c> answers
/// <c>"default"</c> from the same source, so the two can never disagree — an inconsistency between them is
/// one of the oldest ways a page tells a headless browser from a real one.
/// </para>
/// <para>
/// <see cref="StateOf"/> is the one place a permission's state is decided, and therefore the one a future
/// <c>Browser.grantPermissions</c> writes through.
/// </para>
/// </remarks>
internal static class NavigatorPermissions
{
    /// <summary>
    /// https://w3c.github.io/permissions-registry/ — the <c>PermissionName</c> values Chrome's enum accepts
    /// and the registry standardizes. Any other name is a <c>TypeError</c>, as WebIDL converts an enum.
    /// </summary>
    private static readonly FrozenSet<string> _names = new[]
    {
        "accelerometer", "ambient-light-sensor", "background-fetch", "background-sync", "bluetooth", "camera",
        "captured-surface-control", "clipboard-read", "clipboard-write", "display-capture", "fullscreen",
        "geolocation", "gyroscope", "idle-detection", "keyboard-lock", "local-fonts", "magnetometer",
        "microphone", "midi", "nfc", "notifications", "payment-handler", "periodic-background-sync",
        "persistent-storage", "pointer-lock", "push", "screen-wake-lock", "speaker-selection", "storage-access",
        "system-wake-lock", "top-level-storage-access", "window-management",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>The state of the permission <paramref name="name"/> for the page's document.</summary>
    internal static string StateOf(PageRuntime runtime, string name)
    {
        _ = runtime;
        _ = name;
        return "prompt";
    }

    /// <summary>https://w3c.github.io/permissions/#dom-permissions-query</summary>
    /// <remarks>
    /// An operation returning a promise turns every exception into a rejection, including the ones converting
    /// its argument raises, so a missing or unknown <c>name</c> rejects rather than throws.
    /// </remarks>
    internal static JsValue Query(JsSystemObject permissions, JsValue descriptor)
    {
        var runtime = permissions.Runtime;
        var engine = runtime.Engine;
        var realm = engine._mainRealm;
        const string Context = "Failed to execute 'query' on 'Permissions': ";

        if (descriptor is not ObjectInstance dictionary)
        {
            return Reject(runtime, Context + "parameter 1 is not of type 'object'.");
        }

        string name;
        try
        {
            var value = dictionary.Get("name");
            if (value.IsUndefined())
            {
                return Reject(runtime, Context + "Failed to read the 'name' property from 'PermissionDescriptor': Required member is undefined.");
            }

            name = TypeConverter.ToString(value);
        }
        catch (JavaScriptException exception)
        {
            return StreamPromises.RejectedWith(engine, realm, exception.Error);
        }

        if (!_names.Contains(name))
        {
            return Reject(
                runtime,
                Context + "Failed to read the 'name' property from 'PermissionDescriptor': The provided value '" + name
                + "' is not a valid enum value of type PermissionName.");
        }

        var status = new JsPermissionStatus(
            runtime,
            runtime.SystemState.Prototype(SystemInterface.PermissionStatus),
            name,
            StateOf(runtime, name));

        return StreamPromises.ResolvedWith(engine, realm, status);
    }

    private static JsPromise Reject(PageRuntime runtime, string message)
    {
        var realm = runtime.Engine._mainRealm;
        return StreamPromises.RejectedWith(runtime.Engine, realm, realm.Intrinsics.TypeError.Construct(message));
    }
}
