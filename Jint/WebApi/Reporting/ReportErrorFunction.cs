#if NET8_0_OR_GREATER
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Descriptors;

namespace Jint.WebApi.Reporting;

/// <summary>
/// The global <c>reportError(e)</c> function.
/// <para>
/// https://html.spec.whatwg.org/multipage/webappapis.html#dom-reporterror
/// </para>
/// </summary>
/// <remarks>
/// <para>
/// "The <c>reportError(e)</c> method steps are to report an exception <c>e</c> for <b>this</b>." The whole of
/// the operation is therefore
/// <see href="https://html.spec.whatwg.org/multipage/webappapis.html#report-an-exception">report an
/// exception</see>, which for this engine reduces to one step. Its step 5 fires an <c>error</c> event at the
/// global object "if global implements <c>EventTarget</c>" — Jint's global object does not, so nothing is
/// fired, nothing can cancel it, <i>notHandled</i> stays true, and step 6's "the user agent may report
/// exception to a developer console" is what actually happens: the value goes to
/// <see cref="DiagnosticsSink"/>. Script cannot observe or intercept the report.
/// </para>
/// <para>
/// <b>Without a sink this does nothing at all</b>, and it never throws for any value it is given — the report
/// simply has nowhere to go. The one failure it can produce is WebIDL's arity error: <c>e</c> is a required
/// <c>any</c> argument, so <c>reportError()</c> with no arguments is a <c>TypeError</c>, exactly as it is in a
/// browser. https://webidl.spec.whatwg.org/#dfn-create-operation-function
/// </para>
/// </remarks>
internal sealed class ReportErrorFunction : Jint.Native.Function.Function
{
    private static readonly JsString _functionName = new("reportError");

    internal ReportErrorFunction(Engine engine, Realm realm, FunctionPrototype functionPrototype)
        : base(engine, realm, _functionName)
    {
        _prototype = functionPrototype;
        _length = new PropertyDescriptor(JsNumber.Create(1), PropertyFlag.Configurable);
    }

    protected internal override JsValue Call(JsValue thisObject, JsCallArguments arguments)
    {
        if (arguments.Length == 0)
        {
            Throw.TypeError(_realm, "Failed to execute 'reportError': 1 argument required, but only 0 present.");
        }

        // The state is null on an engine that enabled the feature but set no sink, which is precisely the
        // documented no-op: the value was reported, and the report was heard by nobody.
        _engine._webApi?.ReportError(arguments[0]);
        return JsValue.Undefined;
    }
}
#endif
