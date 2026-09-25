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
memory, or process memory. It bounds what the script allocates while it runs, not what the host copies out of
the result afterwards; see [Bound what the host copies out of a result](#bound-what-the-host-copies-out-of-a-result).
`LimitExecutionTime`, statement count, and memory normally reset around each top-level `Execute`, `Evaluate`,
`Invoke`, or `Call`.

A long `a + b` defers its copy, so it is charged when it is built, for the characters it appends, and a `+`
whose result alone would exceed the limit fails at once: no single string a script builds costs more than the
budget to read.

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

## Bound what the host copies out of a result

Strings can share characters: a long `slice` can be a view of the string it was cut from, and a long `a + b`
refers to its operands until something reads it. `LimitMemory` charges each such value only for what it adds,
yet `ToString()` and `ToObject()` copy every value in full, after the operation has ended and outside every
constraint. Under a 16 MB limit, 64 slices of one 1,048,576-character string cost the script about 2 MB, and
`ToObject()` on the array holding them then allocates more than 130 MB.

Read an untrusted result through `Engine.ConvertResult` with `ResultLimits`:

<!-- snippet: guide-bounded-result -->
```csharp
var engine = new Engine(options => options.LimitMemory(16_000_000));
var value = engine.Evaluate(source);

// Unbounded: copies every string in full, whatever it shares.
var copied = value.ToObject();

// Bounded: checks each string's length before copying it.
var detached = engine.ConvertResult(value, ResultLimits.Conservative);
```
<!-- endSnippet -->

`MaxStringLength` is checked against a string's length before its characters are copied, and
`MaxOutputCharacters` stops the conversion once the characters copied so far exceed it, so one conversion
copies no more than the two added together: 3,000,000 characters under `ResultLimits.Conservative`. Set both.
A refusal throws `ResultLimitExceededException`, whose `Limit` names the bound that was reached.

Called without limits, `ConvertResult` uses `Options.ResultLimits`: unlimited by default, and
`UntrustedCodeLimits.ResultLimits` (`ResultLimits.Conservative` unless changed) on an engine built with
`ForUntrustedCode`. Unlimited, it still runs under `LimitMemory` like any engine entry, but a string is charged
only after it has been copied.

## Bound parsing and results

Execution constraints begin after initial parsing. Limit source and AST size separately:

```csharp
options.Parsing.MaxSourceLength = 100_000;
options.Parsing.MaxNodeCount = 25_000;
options.ResultLimits = ResultLimits.Conservative;
```

Prepared code is parsed once and is not reparsed or rechecked on execution. Configure preparation limits when
accepting untrusted source. See [Running untrusted code](./untrusted-code.md) for a complete starting profile.
