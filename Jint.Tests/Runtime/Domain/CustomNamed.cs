namespace Jint.Tests.Runtime.Domain;

[AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
public class CustomNameAttribute : Attribute
{
    public CustomNameAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}

public interface ICustomNamed
{
    [CustomName("jsInterfaceStringProperty")]
    public string InterfaceStringProperty { get; }

    [CustomName("jsInterfaceMethod")]
    public string InterfaceMethod();
}

[CustomName("jsCustomName")]
public class CustomNamed : ICustomNamed
{
    [CustomName("jsStringField")]
    [CustomName("jsStringField2")]
    public string StringField = "StringField";

    [CustomName("jsStaticStringField")]
    public static string StaticStringField = "StaticStringField";

    [CustomName("jsStringProperty")]
    public string StringProperty => "StringProperty";

    [CustomName("jsMethod")]
    public string Method() => "Method";

    [CustomName("jsStaticMethod")]
    public static string StaticMethod() => "StaticMethod";

    public string InterfaceStringProperty => "InterfaceStringProperty";

    public string InterfaceMethod() => "InterfaceMethod";

    [CustomName("jsEnumProperty")]
    public CustomNamedEnum EnumProperty { get; set; }
}

[CustomName("XmlHttpRequest")]
public enum CustomNamedEnum
{
    [CustomName("NONE")]
    None = 0,

    [CustomName("HEADERS_RECEIVED")]
    HeadersReceived = 2
}

public static class CustomNamedExtensions
{
    [CustomName("jsExtensionMethod")]
    public static string ExtensionMethod(this CustomNamed customNamed) => "ExtensionMethod";
}