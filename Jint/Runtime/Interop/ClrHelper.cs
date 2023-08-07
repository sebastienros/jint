using Jint.Native;

namespace Jint.Runtime.Interop
{
    public static class ClrHelper
    {
        public static JsValue ClrUnwrap(JsValue thisObject, JsValue[] arguments)
        {
            var arg = arguments.At(0);
            if (arg is ObjectWrapper obj)
            {
                return new ObjectWrapper(obj.Engine, obj.Target);
            }
            return arg;
        }

        public static JsValue ClrWrap(JsValue thisObject, JsValue[] arguments)
        {
            var arg = arguments.At(0);
            if (arg is ObjectWrapper obj && arguments.At(1) is TypeReference type)
            {
                return new ObjectWrapper(obj.Engine, obj.Target, type.ReferenceType);
            }
            return arg;
        }

        public static JsValue ClrType(JsValue thisObject, JsValue[] arguments)
        {
            var arg = arguments.At(0);
            if (arg is ObjectWrapper obj)
            {
                return TypeReference.CreateTypeReference(obj.Engine, obj.ClrType);
            }
            return JsValue.Null;
        }

        public static JsValue ClrToString(JsValue thisObject, JsValue[] arguments)
        {
            return arguments.At(0).ToString();
        }
    }
}
