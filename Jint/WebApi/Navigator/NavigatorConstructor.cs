#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Navigator;

/// <summary>
/// The <c>Navigator</c> interface object.
/// <para>
/// https://html.spec.whatwg.org/multipage/system-state.html#the-navigator-object
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// The IDL block is <c>[Exposed=Window] interface Navigator { };</c> plus the seven mixins it includes, and it
/// declares <b>no constructor operation</b> — so the interface object exists and is a function but refuses to
/// construct anything, https://webidl.spec.whatwg.org/#es-interface-call. Node 24 answers
/// <c>TypeError: Illegal constructor</c> to <c>new Navigator()</c>, which is the message used here.
/// </para>
/// <para>
/// <b>Why it is a global at all, given <c>[Exposed=Window]</c>.</b> That question was already answered when
/// <c>navigator</c> itself was installed: the object is <c>[Exposed=Window]</c> by the very same declaration,
/// and WinterTC's Minimum Common API asks a non-browser runtime for <c>globalThis.navigator.userAgent</c>
/// anyway. Having decided the instance belongs on a Jint global, withholding the interface object would leave
/// <c>navigator instanceof Navigator</c> unwritable while the object it names sat one link up the prototype
/// chain — the half-truth sebastienros/jint#3250 exists to remove. Node 24, whose global is not a <c>Window</c> either, exposes
/// both for the same reason.
/// </para>
/// </remarks>
internal sealed class NavigatorConstructor : Constructor
{
    private static readonly JsString _functionName = new("Navigator");

    internal NavigatorConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new NavigatorPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal NavigatorPrototype PrototypeObject { get; }

    /// <summary>
    /// An interface without a constructor operation is not constructible.
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        Throw.TypeError(_realm, "Illegal constructor");
        return null!;
    }
}
#endif
