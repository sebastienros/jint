namespace Jint.Native
{
    public class Undefined : IPrimitiveType
    {
        public static object Instance = new Undefined();

        private Undefined()
        {   
        }

        public override string ToString()
        {
            return "undefined";
        }
    }
}
