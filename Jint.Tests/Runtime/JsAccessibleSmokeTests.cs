using System.Reflection;
using Jint.Runtime.Interop;
using Jint.Runtime.Interop.Reflection;

namespace Jint.Tests.Runtime;

[JsAccessible]
public sealed class SmokePlayer
{
    public int Score { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Alive { get; set; }
    public int AddPoints(int delta) { Score += delta; return Score; }
    public string Describe() => $"{Name}:{Score}";
}

public class JsAccessibleSmokeTests
{
    [Fact]
    public void RegistryContainsGeneratedAccessors()
    {
        // Force module-init by touching the type.
        _ = typeof(SmokePlayer);

        Assert.True(GeneratedAccessorRegistry.TryGet(typeof(SmokePlayer), "Score", out var scoreAcc));
        Assert.IsAssignableFrom<GeneratedReflectionAccessor>(scoreAcc);

        Assert.True(GeneratedAccessorRegistry.TryGet(typeof(SmokePlayer), "AddPoints", out var addAcc));
        Assert.IsAssignableFrom<GeneratedMethodAccessor>(addAcc);
    }

    [Fact]
    public void GeneratedPathFiresForPropertyAndMethodAccess()
    {
        var engine = new Engine(cfg => cfg.AllowClr(typeof(SmokePlayer).Assembly));
        var p = new SmokePlayer { Name = "alice", Score = 42, Alive = true };
        engine.SetValue("p", p);

        Assert.Equal(42, (int) (double) engine.Evaluate("p.Score").ToObject()!);
        Assert.Equal("alice", engine.Evaluate("p.Name").ToObject());
        Assert.Equal(true, engine.Evaluate("p.Alive").ToObject());

        engine.Execute("p.Score = 99");
        Assert.Equal(99, p.Score);

        var added = engine.Evaluate("p.AddPoints(5)");
        Assert.Equal(104, (int) (double) added.ToObject()!);
        Assert.Equal(104, p.Score);

        var desc = engine.Evaluate("p.Describe()");
        Assert.Equal("alice:104", desc.ToObject());
    }
}
