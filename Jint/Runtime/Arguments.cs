using Jint.Native;

namespace Jint.Runtime
{
    public static class Arguments
    {
        public static object[] Empty = new object[0];

        public static object[] From(params object[] o)
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
        public static object At(this object[] args, int index, object undefinedValue = null)
        {
            return args.Length > index ? args[index] : undefinedValue ?? Undefined.Instance;
        }
    }
}
