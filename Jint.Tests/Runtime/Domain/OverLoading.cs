namespace Jint.Tests.Runtime.Domain
{
    public class OverLoading
    {
        public string TestFunc(string e)
        {
            return "string";
        }

        public string TestFunc(IntegerEnum e)
        {
            return "integer-enum";
        }

        public string TestFunc(UintEnum e)
        {
            return "uint-enum";
        }
    }
}