using Jint.Browser.Dom.Files;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.WebApi.DomException;
using Jint.WebApi.Files;
using Jint.WebApi.Messaging;
using Jint.WebApi.Navigator;

namespace Jint.Browser.Dom;

// These existing JavaScript objects are not DOM wrappers. Keep their receiver lane
// explicit and finite; DomBindings.Bind/TryBind remain wrapper-only.
internal static class DomNativeJsBindings
{
    internal static DomBinding<JsBlob> BindBlob(JsValue receiver, string member) => Bind<JsBlob>(receiver, member);
    internal static DomBinding<JsFile> BindFile(JsValue receiver, string member) => Bind<JsFile>(receiver, member);
    internal static DomBinding<JsDomException> BindDomException(JsValue receiver, string member) => Bind<JsDomException>(receiver, member);
    internal static DomBinding<JsMessagePort> BindMessagePort(JsValue receiver, string member) => Bind<JsMessagePort>(receiver, member);
    internal static DomBinding<JsNavigator> BindNavigator(JsValue receiver, string member) => Bind<JsNavigator>(receiver, member);
    internal static DomBinding<JsFileList> BindFileList(JsValue receiver, string member) => Bind<JsFileList>(receiver, member);

    private static DomBinding<T> Bind<T>(JsValue receiver, string member) where T : ObjectInstance
    {
        if (receiver is T instance)
        {
            return new(instance, DomRealm.Of(instance.Engine, instance.CreationRealm));
        }

        var message = "Failed to execute '" + member + "': Illegal invocation";
        if (receiver is ObjectInstance other)
        {
            Throw.TypeError(other.CreationRealm, message);
        }
        Throw.TypeErrorNoEngine(message);
        return default;
    }
}
