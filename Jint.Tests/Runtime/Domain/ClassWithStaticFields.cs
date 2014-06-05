namespace Jint.Tests.Runtime.Domain
{
    public class ClassWithStaticFields
    {
        public static string Get = "Get";
        public static string Set = "Set";

        public static string Getter { get { return "Getter"; } }
        public static string Setter { get; set; }

        public static readonly string Readonly = "Readonly";

        static ClassWithStaticFields()
        {
            Setter = "Setter";
        }
    }
}
