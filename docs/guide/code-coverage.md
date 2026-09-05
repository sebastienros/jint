# Code coverage

Enable coverage when constructing the engine:

```csharp
var engine = new Engine(options => options.Coverage.Enabled = true);

engine.Execute("""
    function sign(n) {
        if (n >= 0) return "positive";
        return "negative";
    }
    sign(1);
    """, "rules.js");

foreach (var source in engine.Diagnostics.GetCoverage().Sources)
{
    foreach (var entry in source.Entries)
    {
        Console.WriteLine(
            $"{source.Name}:{entry.Start.Line} {entry.Kind} x{entry.HitCount}");
    }
}
```

Coverage counts entries into executed constructs. A loop statement can be hit repeatedly; a function body is
counted per call and per generator or async resumption.

`Options.Coverage.Granularity` defaults to `CoverageGranularity.Statements`. Set it to
`CoverageGranularity.Functions` to collect function-body entries only.

Only executed constructs appear. The report is a covered set, not a percentage; walk the prepared AST when a
denominator is required. Block statements themselves are omitted.

Counters belong to an engine, not a `Prepared<Script>`, so engines sharing one prepared AST retain independent
measurements. A source represents one parse, even when another parse has the same source name. Reusing a
prepared script keeps one source identity.

Start another measurement with:

```csharp
engine.Diagnostics.ResetCoverage();
```

`GetCoverage` and `ResetCoverage` throw if coverage was not enabled. Collection uses the instrumented
per-statement interpreter path and disables tight-loop optimizations; it is intentionally off by default and
should not be enabled for performance measurements.
