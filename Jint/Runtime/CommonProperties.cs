using Jint.Native;

namespace Jint.Runtime
{
    internal static class CommonProperties
    {
        internal static readonly JsString Arguments = new JsString("arguments");
        internal static readonly JsString Caller = new JsString("caller");
        internal static readonly JsString Callee = new JsString("callee");
        internal static readonly JsString Constructor = new JsString("constructor");
        internal static readonly JsString Eval = new JsString("eval");
        internal static readonly JsString Infinity = new JsString("Infinity");
        internal static readonly JsString Length = new JsString("length");
        internal static readonly JsString Name = new JsString("name");
        internal static readonly JsString Prototype = new JsString("prototype");
        internal static readonly JsString Size = new JsString("size");
        internal static readonly JsString Next = new JsString("next");
        internal static readonly JsString Done = new JsString("done");
        internal static readonly JsString Value = new JsString("value");
        internal static readonly JsString Return = new JsString("return");
        internal static readonly JsString Set = new JsString("set");
        internal static readonly JsString Get = new JsString("get");
        internal static readonly JsString Writable = new JsString("writable");
        internal static readonly JsString Enumerable = new JsString("enumerable");
        internal static readonly JsString Configurable = new JsString("configurable");
        internal static readonly JsString Stack = new JsString("stack");
        internal static readonly JsString Message = new JsString("message");
    }
}
