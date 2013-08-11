namespace Jint.Runtime
{
    public static class Arguments
    {
        public static object[] Empty = new object[0];

        public static object[] From(params object[] o)
        {
            return o;
        }
    }
}
