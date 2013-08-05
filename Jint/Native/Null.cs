namespace Jint.Native
{
    public sealed class Null : IPrimitiveType
    {
        public static dynamic Instance = new Null();

        private Null()
        {   
        }

        public override string ToString()
        {
            return "null";
        }
    }
}
