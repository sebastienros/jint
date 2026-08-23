#if NET8_0_OR_GREATER
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.WebApi.Navigator;

/// <summary>
/// The <c>navigator</c> object — the realm's one instance of the <c>Navigator</c> interface.
/// <para>
/// https://html.spec.whatwg.org/multipage/system-state.html#the-navigator-object
/// </para>
/// </summary>
/// <remarks>
/// It carries no state and no own properties at all: <c>userAgent</c> is <see cref="NavigatorPrototype"/>'s,
/// and what this object <i>is</i> is the brand — the thing that member checks its receiver against, and what
/// <c>navigator instanceof Navigator</c> asks about. Node 24 answers <c>[]</c> to
/// <c>Reflect.ownKeys(navigator)</c> for exactly this reason.
/// </remarks>
internal sealed class JsNavigator : ObjectInstance
{
    private JsNavigator(Engine engine) : base(engine, ObjectClass.Object)
    {
    }

    internal static JsNavigator Create(Engine engine, Realm realm) => new(engine)
    {
        _prototype = realm.Intrinsics.Navigator.PrototypeObject,
    };
}
#endif
