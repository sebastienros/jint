# Execution constraints

Configure several independent bounds; no single constraint is a complete sandbox:

```csharp
var engine = new Engine(options =>
{
    options.LimitStatements(100_000);
    options.LimitMemory(16_000_000);
    options.LimitExecutionTime(TimeSpan.FromSeconds(2));
    options.ObserveCancellation(cancellationToken);

    options.Constraints.MaxRecursionDepth = 64;
    options.Constraints.MaxArraySize = 100_000;
    options.Constraints.RegexTimeout = TimeSpan.FromMilliseconds(250);
    options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(2);
});
```

Constraints are cooperative. They run at interpreter check points and cannot preempt a host callback or BCL
operation that does not return. Keep an outer request or worker deadline and use operating-system CPU and memory
limits for a hard boundary.

`LimitMemory` measures managed allocations while an engine operation is active, not retained heap, unmanaged
memory, or process memory. `LimitExecutionTime`, statement count, and memory normally reset around each
top-level `Execute`, `Evaluate`, `Invoke`, or `Call`.

The native stack-overflow guard is enabled by default. It converts exhausted recursion headroom into a
catchable JavaScript `RangeError` while the engine can still unwind. `MaxRecursionDepth` is an additional,
configured recursion bound; it does not replace the native-stack guard for every recursion shape.

Non-positive or saturated values such as `int.MaxValue`, `long.MaxValue`, and `TimeSpan.MaxValue` disable the
corresponding built-in limit rather than creating an effectively large one.

## Bound a multi-entry host operation

Ordinary limits cover one engine entry, not a host loop that calls into the engine repeatedly. Bracket that
operation with `OperationDeadlineConstraint`:

```csharp
var deadline = new OperationDeadlineConstraint();
var engine = new Engine(options => options.AddConstraint(deadline));

deadline.Begin(TimeSpan.FromSeconds(2), cancellationToken);
try
{
    foreach (var row in rows)
    {
        engine.Invoke("render", row);
    }
}
finally
{
    deadline.End();
}
```

The same two-second deadline spans the complete loop. A `MemoryLimitConstraint` can likewise be retrieved with
`engine.Constraints.Find<MemoryLimitConstraint>()` and bracketed with `Begin`/`End` to preserve one cumulative
allocation budget.

Alternatively, move the loop into one JavaScript evaluation or enforce the total budget in host code.

## Bound parsing and results

Execution constraints begin after initial parsing. Limit source and AST size separately:

```csharp
options.Parsing.MaxSourceLength = 100_000;
options.Parsing.MaxNodeCount = 25_000;
options.ResultLimits = ResultLimits.Conservative;
```

Prepared code is parsed once and is not reparsed or rechecked on execution. Configure preparation limits when
accepting untrusted source. See [Running untrusted code](./untrusted-code.md) for a complete starting profile.
