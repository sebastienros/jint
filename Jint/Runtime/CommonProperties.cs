using Jint.Native;

namespace Jint.Runtime
{
    internal static class CommonProperties
    {
        internal static readonly JsString Arguments = (JsString) "arguments";
        internal static readonly JsString Caller = (JsString) "caller";
        internal static readonly JsString Callee = (JsString) "callee";
        internal static readonly JsString Constructor = (JsString) "constructor";
        internal static readonly JsString Eval = (JsString) "eval";
        internal static readonly JsString Infinity = (JsString) "Infinity";
        internal static readonly JsString Length = (JsString) "length";
        internal static readonly JsString Name = (JsString) "name";
        internal static readonly JsString Prototype = (JsString) "prototype";
        internal static readonly JsString Size =  (JsString) "size";
        internal static readonly JsString Next =  (JsString) "next";
    }
}