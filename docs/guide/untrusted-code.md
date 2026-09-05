# Running untrusted code

Jint is an in-process interpreter, not an operating-system security boundary. Safe hosting needs a hardened
engine configuration, a minimal capability surface, bounded inputs and outputs, fresh engines across trust
domains, and disposable process-level isolation.

## Hardened profile

`ForUntrustedCode` disables broad CLR and reflection access, module loading, debugger handling, string
compilation, projected CLR writes, live CLR-array views, registered extension methods, and blocking
`Atomics.wait`. It enables the native stack guard and applies finite resource limits.

Start with the conservative profile and tune it for the workload:

```csharp
var limits = UntrustedCodeLimits.Default with
{
    TimeoutInterval = TimeSpan.FromSeconds(1),
    MaxStatements = 50_000,
    MemoryLimit = 16_000_000
};

var options = new Options().ForUntrustedCode(limits);
options.Strict = true;

using var engine = new Engine(options);

using (limits.BeginOperation(engine, requestAborted))
{
    var value = engine.Evaluate(source);
    return engine.ConvertResult(value, limits.ResultLimits);
}
```

`BeginOperation` requires a cancellable token and adds one cumulative deadline and allocation budget across
evaluation, callbacks, result conversion, Jint JSON serialization, and bounded error rendering. Per-entry
statement and timeout limits still reset for each engine call. Do not end the scope while an async operation
still owns the engine.

## Validate a custom policy

If you build options manually, validate the final configuration:

```csharp
options.EnsureSecurityConfiguration(
    SecurityConfigurationPolicy.UntrustedScripts);

var engine = new Engine(options);
var report = engine.Diagnostics.ValidateSecurityConfiguration(
    SecurityConfigurationPolicy.UntrustedScripts);
```

Validation reports stable diagnostics but changes nothing. A clean report is not proof of isolation; callbacks,
custom loaders, serializers, network clients, output sinks, and process limits remain host responsibilities.

## Host responsibilities

- Do not enable CLR namespace access, reflection, projected mutators, or network APIs unless they are required
  capabilities.
- Bound source length and AST nodes before execution, module graphs and redirects at their loaders, and result,
  serialized, logged, and response sizes at their sinks.
- Keep an external hard deadline. Cooperative constraints cannot interrupt arbitrary host callbacks.
- Use separate engines for mutually distrusting scripts. Global snapshots do not undo intrinsic, module,
  reachable-object, or CLR-state mutations.
- Leave `AgentCanSuspend` false on request, UI, and event-loop threads. Otherwise `Atomics.wait` without a
  timeout can block that thread indefinitely.
- Run hostile parsing and execution in a least-privileged disposable worker with OS CPU, memory, filesystem,
  network, and lifetime controls.

See [Execution constraints](./constraints.md), [CLR interop](./clr-interop.md), and
[Web APIs](./web-apis.md).
