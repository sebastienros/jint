#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Fetch;

/// <summary>
/// The <c>Headers</c> interface object.
/// <para>
/// https://fetch.spec.whatwg.org/#headers-class
/// </para>
/// </summary>
/// <remarks>
/// <c>constructor(optional HeadersInit init)</c>. As a WebIDL interface object its <c>[[Prototype]]</c> is
/// <c>%Function.prototype%</c> and calling it without <c>new</c> raises a <c>TypeError</c>, which
/// <see cref="Constructor"/> already does.
/// </remarks>
internal sealed class HeadersConstructor : Constructor
{
    private static readonly JsString _functionName = new("Headers");

    internal HeadersConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new HeadersPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal HeadersPrototype PrototypeObject { get; }

    /// <summary>
    /// https://fetch.spec.whatwg.org/#dom-headers — "the new Headers(init) constructor steps are to set this's
    /// guard to 'none' and then fill this with init".
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        var headers = OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.Headers.PrototypeObject,
            static (Engine engine, Realm _, HeaderList? state) => new JsHeaders(engine, state!),
            new HeaderList());

        // init is optional with no default value, so an explicitly passed `undefined` means "not present"
        // and the object stays empty — https://webidl.spec.whatwg.org/#es-overloads.
        var init = arguments.At(0);
        if (!init.IsUndefined())
        {
            headers.Fill(_realm, init);
        }

        return headers;
    }

    /// <summary>
    /// Builds a <c>Headers</c> object over an existing list, for the <c>Request</c> and <c>Response</c>
    /// constructors and for a response the network produced.
    /// </summary>
    internal JsHeaders CreateInstance(HeaderList list) => new(_engine, list) { _prototype = PrototypeObject };
}
#endif
