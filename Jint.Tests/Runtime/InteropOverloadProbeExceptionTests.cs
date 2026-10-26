#nullable enable
using System.Runtime.ExceptionServices;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins that scoring an overload never throws and catches an exception (#4337). A candidate whose parameter type
/// the scorer has no rule for falls back to <c>TypeConverter.TryConvert</c>, which used to reach
/// <c>Convert.ChangeType</c> with a value that is neither <see cref="IConvertible"/> nor of the target type -
/// a call that can only throw <see cref="InvalidCastException"/>.
/// </summary>
public class InteropOverloadProbeExceptionTests
{
    public readonly record struct Vec2(double X, double Y);

    public sealed class Entity
    {
        public string Name { get; set; } = "e";
    }

    public sealed class Api
    {
        public string Move(Vec2 position) => "vec";

        public string Move(Entity entity) => "entity:" + entity.Name;
    }

    [Fact]
    public void OverloadedCallDoesNotThrowFirstChanceExceptions()
    {
        var engine = new Engine();
        engine.SetValue("api", new Api());
        engine.SetValue("ent", new Entity());

        var script = Engine.PrepareScript("api.Move(ent)");
        Assert.Equal("entity:e", engine.Evaluate(script).AsString());

        var thread = Environment.CurrentManagedThreadId;
        var count = 0;
        void Handler(object? sender, FirstChanceExceptionEventArgs e)
        {
            if (Environment.CurrentManagedThreadId == thread)
            {
                count++;
            }
        }

        AppDomain.CurrentDomain.FirstChanceException += Handler;
        try
        {
            for (var i = 0; i < 100; i++)
            {
                Assert.Equal("entity:e", engine.Evaluate(script).AsString());
            }
        }
        finally
        {
            AppDomain.CurrentDomain.FirstChanceException -= Handler;
        }

        Assert.Equal(0, count);
    }
}
