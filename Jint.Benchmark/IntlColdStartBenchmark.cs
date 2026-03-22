using BenchmarkDotNet.Attributes;
using Jint.Native;

namespace Jint.Benchmark;

[MemoryDiagnoser]
public class IntlColdStartBenchmark
{
    [Benchmark]
    public JsValue IntlNumberFormat()
    {
        var engine = new Engine();
        return engine.Evaluate("new Intl.NumberFormat('de', {notation: 'compact'}).format(1234567)");
    }

    [Benchmark]
    public JsValue IntlDateTimeFormat()
    {
        var engine = new Engine();
        return engine.Evaluate("new Intl.DateTimeFormat('en').format(new Date())");
    }

    [Benchmark]
    public JsValue IntlListFormat()
    {
        var engine = new Engine();
        return engine.Evaluate("new Intl.ListFormat('en').format(['a','b','c'])");
    }

    [Benchmark]
    public JsValue IntlRelativeTimeFormat()
    {
        var engine = new Engine();
        return engine.Evaluate("new Intl.RelativeTimeFormat('en').format(-1, 'day')");
    }

    [Benchmark]
    public JsValue LocaleCanonicalization()
    {
        var engine = new Engine();
        return engine.Evaluate("new Intl.Locale('en-US').maximize().toString()");
    }
}
