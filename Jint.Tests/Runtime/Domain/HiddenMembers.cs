namespace Jint.Tests.Runtime.Domain
{
    public class HiddenMembers
    {
        public string Member1 { get; set; } = "Member1";
        public string Member2 { get; set; } = "Member2";
        public string Method1()
        {
            return "Method1";
        }
        public string Method2()
        {
            return "Method2";
        }
    }
}
