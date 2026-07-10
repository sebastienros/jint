using Jint.Runtime;

namespace Jint.Tests.Runtime;

/// <summary>
/// Pins the semantics of the JintNewExpression call-site constructor cache: identity-validated,
/// falls through on any miss, and the zero-arg leaf fast path (default-time-system Date) stays
/// unobservable.
/// </summary>
public class NewExpressionCacheTests
{
    [Fact]
    public void RepeatedNewDateUsesRealmPrototype()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                var ok = 0;
                var last = null;
                for (var i = 0; i < 1000; i++) {
                    var d = new Date();
                    if (Object.getPrototypeOf(d) === Date.prototype && d !== last) ok++;
                    last = d;
                }
                return ok;
            })()
            """).AsNumber();

        Assert.Equal(1000, result);
    }

    [Fact]
    public void ReassignedDateConstructorIsPickedUpAtWarmCallSite()
    {
        var engine = new Engine();
        // warm the call site with the built-in Date, then swap the global binding: the cache
        // must miss on identity and construct through the new function
        var result = engine.Evaluate("""
            (function () {
                var seen = '';
                function make() { return new Date(); }
                for (var i = 0; i < 100; i++) make();
                seen += (make() instanceof Date);
                Date = function () { this.marker = 42; };
                var d = make();
                seen += ':' + d.marker;
                return seen;
            })()
            """).AsString();

        Assert.Equal("true:42", result);
    }

    [Fact]
    public void SubclassedDateGetsSubclassPrototype()
    {
        var engine = new Engine();
        var result = engine.Evaluate("""
            (function () {
                for (var i = 0; i < 100; i++) new Date();
                class D extends Date { }
                var d = new D();
                return (Object.getPrototypeOf(d) === D.prototype) + ':' + (d instanceof Date);
            })()
            """).AsString();

        Assert.Equal("true:true", result);
    }

    [Fact]
    public void SharedPreparedScriptConstructsAgainstOwnRealm()
    {
        // one prepared script executed by two engines: the call-site cache must re-resolve per
        // engine (identity miss) so each engine gets dates from its own realm intrinsics
        var prepared = Engine.PrepareScript("""
            var d = new Date();
            Object.getPrototypeOf(d) === Date.prototype;
            """);

        var engine1 = new Engine();
        var engine2 = new Engine();
        for (var i = 0; i < 5; i++)
        {
            Assert.True(engine1.Evaluate(prepared).AsBoolean());
            Assert.True(engine2.Evaluate(prepared).AsBoolean());
        }
    }

    [Fact]
    public void CustomTimeSystemStillServesNewDate()
    {
        // a custom ITimeSystem disables the leaf fast path (exact-type gate); constructs run the
        // generic path and observe the custom clock
        var engine = new Engine(options => options.TimeSystem = new FixedTimeSystem());
        var result = engine.Evaluate("""
            (function () {
                var ok = 0;
                for (var i = 0; i < 100; i++) {
                    if (new Date().getTime() === 123456789) ok++;
                }
                return ok;
            })()
            """).AsNumber();

        Assert.Equal(100, result);
    }

    [Fact]
    public void ThrowingCustomTimeSystemSurfacesExceptionPerConstruct()
    {
        var engine = new Engine(options => options.TimeSystem = new ThrowingTimeSystem());
        var ex = Assert.Throws<InvalidOperationException>(() => engine.Evaluate("new Date()"));
        Assert.Equal("clock is broken", ex.Message);
    }

    private sealed class FixedTimeSystem : ITimeSystem
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeMilliseconds(123456789);
        public TimeZoneInfo DefaultTimeZone => TimeZoneInfo.Utc;
        public bool TryParse(string date, out long epochMilliseconds)
        {
            epochMilliseconds = 0;
            return false;
        }
    }

    private sealed class ThrowingTimeSystem : ITimeSystem
    {
        public DateTimeOffset GetUtcNow() => throw new InvalidOperationException("clock is broken");
        public TimeZoneInfo DefaultTimeZone => TimeZoneInfo.Utc;
        public bool TryParse(string date, out long epochMilliseconds)
        {
            epochMilliseconds = 0;
            return false;
        }
    }
}
