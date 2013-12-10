using Jint.Native;

namespace Jint.Runtime
{
    public static class Arguments
    {
        public static JsValue[] Empty = new JsValue[0];

        public static JsValue[] From(params JsValue[] o)
        {
            return o;
        }

        /// <summary>
        /// Returns the arguments at the provided position or Undefined if not present
        /// </summary>
        /// <param name="args"></param>
        /// <param name="index">The index of the parameter to return</param>
        /// <param name="undefinedValue">The value to return is the parameter is not provided</param>
        /// <returns></returns>
        public static JsValue At(this JsValue[] args, int index, JsValue undefinedValue)
        {
            return args.Length > index ? args[index] : undefinedValue;
        }

        public static JsValue At(this JsValue[] args, int index)
        {
            return At(args, index, Undefined.Instance);
        }
    }
}
