#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Files;

/// <summary>
/// The <c>FormData</c> interface object.
/// <para>
/// https://xhr.spec.whatwg.org/#interface-formdata
/// </para>
/// </summary>
/// <remarks>
/// The IDL constructor is <c>constructor(optional HTMLFormElement form, optional HTMLElement? submitter = null)</c>,
/// supplied forms are delegated to the browser's entry-list builder when it installs one for this realm.
/// Without that connection, a non-<c>undefined</c> first argument raises a <c>TypeError</c>, preserving
/// WinterTC's Minimum Common Web Platform API behavior for a runtime without a DOM.
/// </remarks>
internal sealed class FormDataConstructor : Constructor
{
    private static readonly JsString _functionName = new("FormData");

    internal FormDataConstructor(
        Engine engine,
        Realm realm,
        FunctionPrototype functionPrototype,
        ObjectPrototype objectPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        PrototypeObject = new FormDataPrototype(engine, realm, this, objectPrototype);
        _length = new PropertyDescriptor(JsNumber.PositiveZero, PropertyFlag.Configurable);
        _prototypeDescriptor = new PropertyDescriptor(PrototypeObject, PropertyFlag.AllForbidden);
    }

    internal FormDataPrototype PrototypeObject { get; }

    // The browser installs this on its own realm's constructor; workers and standalone engines retain
    // the no-DOM behavior. The engine assembly never needs a reference to a DOM type.
    internal Func<JsValue, JsValue, JsValue, JsFormData>? ConstructFromForm { get; set; }

    /// <summary>
    /// https://xhr.spec.whatwg.org/#dom-formdata
    /// </summary>
    public override ObjectInstance Construct(JsCallArguments arguments, JsValue newTarget)
    {
        if (newTarget.IsUndefined())
        {
            Throw.TypeError(_realm, $"Constructor {GetOwnFunctionNameForMessage()} requires 'new'");
        }

        if (ConstructFromForm is { } construct)
        {
            return construct(arguments.At(0), arguments.At(1), newTarget);
        }

        if (!arguments.At(0).IsUndefined())
        {
            Throw.TypeError(_realm, "FormData constructor: the form argument is not supported, there is no DOM");
        }

        return CreateInstance(newTarget);
    }

    internal JsFormData CreateInstance(JsValue newTarget)
        => OrdinaryCreateFromConstructor(
            newTarget,
            static intrinsics => intrinsics.FormData.PrototypeObject,
            static (Engine engine, Realm _, object? _) => new JsFormData(engine),
            state: null);
}
#endif
