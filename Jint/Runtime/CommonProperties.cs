using Jint.Native;

namespace Jint.Runtime;

internal static class CommonProperties
{
    internal static readonly JsString Arguments = JsString.CachedCreate("arguments");
    internal static readonly JsString Callee = JsString.CachedCreate("callee");
    internal static readonly JsString Caller = JsString.CachedCreate("caller");
    internal static readonly JsString Configurable = JsString.CachedCreate("configurable");
    internal static readonly JsString Constructor = JsString.CachedCreate("constructor");
    internal static readonly JsString Done = JsString.CachedCreate("done");
    internal static readonly JsString Enumerable = JsString.CachedCreate("enumerable");
    internal static readonly JsString Eval = JsString.CachedCreate("eval");
    internal static readonly JsString Get = JsString.CachedCreate("get");
    internal static readonly JsString Has = JsString.CachedCreate("has");
    internal static readonly JsString Infinity = JsString.CachedCreate("Infinity");
    internal static readonly JsString Keys = JsString.CachedCreate("keys");
    internal static readonly JsString Length = JsString.CachedCreate("length");
    internal static readonly JsString Message = JsString.CachedCreate("message");
    internal static readonly JsString Name = JsString.CachedCreate("name");
    internal static readonly JsString Next = JsString.CachedCreate("next");
    internal static readonly JsString Prototype = JsString.CachedCreate("prototype");
    internal static readonly JsString Return = JsString.CachedCreate("return");
    internal static readonly JsString Set = JsString.CachedCreate("set");
    internal static readonly JsString Size = JsString.CachedCreate("size");
    internal static readonly JsString Stack = JsString.CachedCreate("stack");
    internal static readonly JsString Value = JsString.CachedCreate("value");
    internal static readonly JsString Writable = JsString.CachedCreate("writable");
}
