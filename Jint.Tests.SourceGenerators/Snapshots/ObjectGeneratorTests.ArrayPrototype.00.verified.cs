//HintName: Attributes.g.cs

namespace Jint;

[System.AttributeUsage(System.AttributeTargets.Class)]
internal class JsObjectAttribute : System.Attribute
{
}

[System.AttributeUsage(System.AttributeTargets.Method)]
internal class JsFunctionAttribute : System.Attribute
{
    public string Name { get; set; }
    public int Length { get; set; }
}

[System.AttributeUsage(System.AttributeTargets.Property)]
internal class JsPropertyAttribute : System.Attribute
{
    public string Name { get; set; }
    public Jint.Runtime.Descriptors.PropertyFlag Flags { get; set; }
}
