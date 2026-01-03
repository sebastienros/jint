namespace Jint.Tests.Runtime.Domain;

public class HiddenMembers
{
    public HiddenMembers()
    {
    }

    public HiddenMembers(string member1)
    {
        Member1 = member1;
    }

    [Obsolete]
    public string Field1 = "Field1";

    public string Field2 = "Field2";

    [Obsolete]
    public string Member1 { get; set; } = "Member1";

    public string Member2 { get; set; } = "Member2";

    [Obsolete]
    public string Method1() => "Method1";

    public string Method2() => "Method2";

    public Type Type => GetType();
}
