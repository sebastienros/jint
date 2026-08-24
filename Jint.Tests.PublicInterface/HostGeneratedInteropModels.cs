#nullable enable

using Jint.Native;

namespace Jint.Tests.PublicInterface;

/// <summary>
/// The fixture for <see cref="HostGeneratedInteropTests"/>: one annotated type and one identical un-annotated
/// twin, so every assertion can be made twice and compared.
/// <para>
/// These live in <c>Jint.Tests.PublicInterface</c> deliberately. It is the only suite without
/// <c>InternalsVisibleTo</c>, so the fact that this file compiles at all is the claim under test — the code
/// the generator emits for these types reaches nothing but Jint's public surface.
/// </para>
/// </summary>
[JsAccessible]
public sealed class GeneratedModel
{
    public int Score { get; set; }
    public long Ticks { get; set; }
    public double Ratio { get; set; }
    public bool Active { get; set; }
    public string? Name { get; set; }
    public JsValue? Payload { get; set; }
    public JsString? Tag { get; set; }
    public string[]? Tags { get; set; }

    public int Doubled => Score * 2;

    public int Echoed { set { Score = value; } }

    public int Counter;
    public readonly string Stamped = "stamped";

    /// <summary>Reflection writes a non-public accessor; generated code cannot, so this stays reflected.</summary>
    public int Hidden { get; private set; }

    public JsValue Echo(JsValue value) => value ?? JsValue.Undefined;

    public void Touch(JsValue value) => Touched = value;

    public int Count() => Score;

    public string Describe(JsValue first, JsValue second) => first + "/" + second;

    /// <summary>Overloaded, so the generator declines the name entirely and reflection keeps it.</summary>
    public int Add(int value) => value;

    public int Add(int first, int second) => first + second;

    /// <summary>A non-JsValue parameter, whose reflected binding is a conversion the generator will not reproduce.</summary>
    public string Shout(string text) => text.ToUpperInvariant();

    public JsValue? Touched { get; private set; }

    public void SetHidden(int value) => Hidden = value;
}

/// <summary>Byte-for-byte the same members as <see cref="GeneratedModel"/>, without the attribute.</summary>
public sealed class ReflectedModel
{
    public int Score { get; set; }
    public long Ticks { get; set; }
    public double Ratio { get; set; }
    public bool Active { get; set; }
    public string? Name { get; set; }
    public JsValue? Payload { get; set; }
    public JsString? Tag { get; set; }
    public string[]? Tags { get; set; }

    public int Doubled => Score * 2;

    public int Echoed { set { Score = value; } }

    public int Counter;
    public readonly string Stamped = "stamped";

    public int Hidden { get; private set; }

    public JsValue Echo(JsValue value) => value ?? JsValue.Undefined;

    public void Touch(JsValue value) => Touched = value;

    public int Count() => Score;

    public string Describe(JsValue first, JsValue second) => first + "/" + second;

    public int Add(int value) => value;

    public int Add(int first, int second) => first + second;

    public string Shout(string text) => text.ToUpperInvariant();

    public JsValue? Touched { get; private set; }

    public void SetHidden(int value) => Hidden = value;
}

/// <summary>
/// A nested annotated type. Its hint name has to carry the containing type: the MVP derived one from
/// <c>{Namespace}.{Name}</c> alone, so this type and a top-level <c>Player</c> in the same namespace both
/// claimed <c>Ns.Player.JsAccessible.g.cs</c> and the second <c>AddSource</c> threw.
/// </summary>
public static class GeneratedOuter
{
    [JsAccessible]
    public sealed class Player
    {
        public string? Name { get; set; }
    }
}

/// <summary>A top-level type of the same name, which is what makes the nesting matter.</summary>
[JsAccessible]
public sealed class Player
{
    public string? Name { get; set; }
}

/// <summary>
/// A partial declaration. The attribute can only be written on one part — <c>AllowMultiple</c> is false, so
/// writing it twice is CS0579 rather than anything the generator could see — and the members of every part
/// have to be picked up from the one symbol it does see.
/// </summary>
[JsAccessible]
public sealed partial class PartiallyAnnotated
{
    public int First { get; set; }
}

public sealed partial class PartiallyAnnotated
{
    public int Second { get; set; }
}
