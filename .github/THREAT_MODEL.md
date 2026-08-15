# Threat model: untrusted JavaScript on a server

## Status and scope

This document models a server that accepts JavaScript from an untrusted end user and
executes it with Jint in the server process. The attacker may submit any valid or invalid
source, repeat requests, coordinate requests, observe returned values, errors, timing, and
resource exhaustion, and use every JavaScript feature that the host leaves enabled.

The assets in scope are:

- availability and integrity of the server process;
- filesystem, environment variables, credentials, network access, and other authority held
  by the process;
- host objects, delegates, services, and module loaders exposed to the engine;
- confidentiality and integrity between users, tenants, and requests;
- request CPU time, memory, threads, handles, logs, and output bandwidth.

Jint, Acornima, the .NET runtime, the host integration, and configured module sources are
inside the trusted computing base. Compromise of the operating system or a malicious host
application is out of scope.

This is a living model, not a claim that the list is exhaustive. New JavaScript features,
interop APIs, module loaders, and host callbacks must be reviewed against the trust
boundaries below.

## Security conclusion

Jint is an **in-process interpreter, not an operating-system security boundary**. It has
useful capability defaults and cooperative resource constraints, but script and host still
share one process, identity, address space, garbage collector, and thread pool. A runtime
bug, an unsafe capability, an uninterruptible host call, or an incorrectly configured
limit can affect the whole worker.

For hostile or multi-tenant code, use both layers:

1. Configure Jint with a minimal capability surface and explicit resource limits.
2. Run it in a disposable, least-privileged worker process or container with independent
   CPU, memory, wall-clock, filesystem, and network limits. Terminating that worker is the
   only hard preemption mechanism.

Run mutually distrusting scripts in separate `Engine` instances. A global snapshot is a
reuse optimization, not a sandbox reset.

## Architecture and trust boundaries

```text
Untrusted user
  | source, module specifiers, arguments
  v
Server input handling
  | bounded input
  v
Jint parser (Acornima) -> interpreter -> promises/event loop
  |                         |                 |
  |                         |                 +-> host Tasks / async module loads
  |                         +-> projected objects, delegates, converters, debugger
  +-> module loader -> filesystem or network

All components above run with the hosting worker's process authority.
```

| Boundary | Attacker-controlled input | Security decision |
| --- | --- | --- |
| Request to parser | Script text, source name, parsing options | Input size, parsing isolation, source retention |
| Script to interpreter | Statements, objects, loops, recursion, regexes, promises | Time, statement, memory, stack, and collection limits |
| Script to host | Projected objects, delegates, CLR types, callback arguments | Capability design, authorization, mutability, cancellation |
| Script to module loader | Specifiers, import attributes, import graph | Scheme/origin/path policy, byte limits, timeouts, secret-free names |
| Async completion to engine | Task results, module loads, timers, promise jobs | Thread ownership, cancellation, event-loop generation |
| One request to another | Engine, realm, globals, intrinsics, host state, cached values | Engine lifetime and tenant isolation |
| Result to server response | Values, errors, stack traces, serialized graphs | Redaction, output size, serialization time, log safety |

## Security-relevant defaults

Defaults are compatibility choices, not a hardened profile.

| Control | Default | Security effect |
| --- | --- | --- |
| CLR namespace access (`Interop.Enabled`) | Disabled | `System`, `importNamespace`, and `clrHelper` are not installed |
| `AllowGetType`, `AllowSystemReflection` | Disabled | Blocks important reflection and allow-list bypass paths |
| Writes through projected CLR objects | Enabled | A default engine can mutate host objects passed with `SetValue` |
| CLR array conversion | Live view | Script writes can mutate a projected CLR array |
| Modules | Disabled | The fail-fast loader rejects imports |
| `require` | Disabled | No CommonJS-like loader global |
| Timeout, statement, memory, recursion limits | None | Untrusted execution is unbounded unless the host opts in |
| Stack overflow guard | Disabled | Native stack exhaustion can terminate the process |
| Maximum array size | `uint.MaxValue` | Effectively unbounded for hostile input |
| Regex timeout | 10 seconds | Bounds an individual regular expression operation |
| Promise wait timeout | 10 seconds | Bounds host APIs that wait for promise or module settlement |
| Dynamic `eval` / `Function` compilation | Enabled | Script can create and parse additional source at runtime |
| `Atomics.wait` suspension | Enabled | Script may block the engine thread, including indefinitely |
| Debugger | Disabled | Debug callbacks and debugger expressions are inactive |
| Detailed CLR resolution errors | Disabled | Reduces script-visible CLR surface disclosure |
| Function source retention | Disabled | Submitted function source is not retained solely for `toString()` |

## Threat inventory

### TM-01: CLR access becomes process authority

**Threat.** Enabling CLR interop lets script reach powerful .NET APIs. Core library types
can be resolved even when `AllowedAssemblies` is empty or narrow, so that collection must
not be treated as a complete sandbox allow-list. Enabling `AllowGetType` can open further
type-resolution and reflection paths. The result can be filesystem or environment access,
network access, process creation, native interop, or arbitrary code execution with the
worker's identity.

**Existing mitigations.**

- CLR namespace access, `AllowGetType`, and `AllowSystemReflection` are disabled by default.
- `TypeResolver.MemberFilter` can filter projected members.
- `ExposeDetailedResolutionErrors` is disabled by default.

**Missing or residual mitigation.**

- Jint cannot reduce the operating-system authority of an enabled CLR API.
- `AllowedAssemblies` is not a complete capability boundary for core runtime types.
- A member filter is host code and can be incomplete or become stale as types evolve.

**Required host action.** Never call `AllowClr` for untrusted scripts. Never enable
`AllowGetType` or `AllowSystemReflection`. If CLR projection is unavoidable, use a dedicated
allow-listing `TypeResolver`, test it as a security boundary, and still isolate the worker at
the operating-system level.

### TM-02: Projected host objects and delegates act as ambient capabilities

**Threat.** `SetValue`, lazy globals, module exports, converters, resolvers, and host
callbacks can expose database, filesystem, HTTP, service-provider, identity, or secret
objects even while CLR namespace access is disabled. Script controls callback arguments,
order, and frequency, which can create confused-deputy, authorization bypass, SSRF,
exfiltration, or resource-amplification paths.

**Existing mitigations.**

- The host chooses every projected value.
- `TypeResolver.MemberFilter`, wrapper handlers, proxies, and narrow delegates can reduce
  the surface.

**Missing or residual mitigation.**

- `Interop.Enabled = false` does not make projected CLR objects inert.
- Jint cannot infer authorization, request scope, safe URLs, query cost, or output bounds.
- User converters and factories are host code and are not automatically interruptible.

**Required host action.** Expose purpose-built, least-authority functions or immutable data,
not service providers or general domain objects. Re-authorize every operation inside the
callback, validate all arguments, cap call frequency and result size, propagate
cancellation, and avoid returning powerful objects.

### TM-03: Script mutates host state

**Threat.** Writes through an `ObjectWrapper` are enabled by default. Projected CLR arrays
are live views by default. A script used only for "read-only" rules or templates can
therefore alter host properties, fields, collections, arrays, or objects reachable from
them, potentially violating host invariants.

**Existing mitigations.**

- `AllowClrWrite(false)` blocks writes through CLR wrappers.
- `ArrayConversionMode.Copy`, immutable DTOs, and Jint-owned record layouts avoid live
  write-through.

**Missing or residual mitigation.**

- Read-only wrapper configuration does not make a mutable object graph immutable outside
  Jint.
- Calling exposed methods can still mutate state even when property writes are disabled.

**Required host action.** Set `AllowClrWrite(false)`, use copied arrays, and expose immutable
snapshots. Do not expose mutating methods unless they are intentional, authorized
capabilities.

### TM-04: Infinite loops and computational denial of service

**Threat.** Loops, generators, proxies, coercion hooks, large collection operations,
BigInt/string arithmetic, and adversarial algorithms can monopolize a worker.

**Existing mitigations.**

- `TimeoutInterval`, `MaxStatements`, and `CancellationToken` constraints.
- Long-running built-ins periodically call the constraint set.
- Amortized timeout and cancellation checks are also made after CLR calls return.

**Missing or residual mitigation.**

- No execution constraint is registered by default.
- Constraints are cooperative checkpoints, not preemptive scheduling.
- A statement cap is not a CPU cap: one statement or host call can do substantial work.
- Detection can occur after a configured wall-clock deadline.

**Required host action.** Configure time, statement, and cancellation constraints together.
Apply a request-level deadline outside Jint, rate-limit submissions, and enforce an
independent process/container CPU and wall-clock limit.

### TM-05: Parsing and dynamic compilation denial of service

**Threat.** Very large or adversarial scripts, source offsets, modules, `eval` strings, and
`Function` constructor strings consume CPU and memory while parsing.

**Existing mitigations.**

- Parser stack failures are translated to managed errors.
- Dynamic string compilation can be disabled.
- Function source text retention is disabled by default.

**Missing or residual mitigation.**

- Initial `Execute(string)` and `Evaluate(string)` parse before
  `ExecuteWithConstraints`; execution timeout, statement, and memory constraints do not
  bound that parse.
- There is no Jint option for maximum script bytes, tokens, AST nodes, or total imported
  source.
- Dynamic parsing that occurs during execution cannot be preempted while the parser is
  running.

**Required host action.** Reject oversized source before Jint, cap source offsets, module
count and bytes, and parse in the disposable worker. Disable string compilation unless it
is a requirement.

### TM-06: Memory exhaustion

**Threat.** Script can allocate strings, arrays, typed arrays, object graphs, closures,
promises, modules, symbols, and host wrappers until the process is out of memory or spends
most of its time in garbage collection.

**Existing mitigations.**

- `LimitMemory` measures allocations for a top-level engine entry.
- `MaxArraySize` bounds Jint array creation when configured.
- Some built-ins reject impossible allocations.
- Recent-wrapper caching is bounded by default; the unbounded identity map is opt-in.

**Missing or residual mitigation.**

- No memory or practical array limit is configured by default.
- The memory constraint measures allocations on the current thread, not retained heap or
  process memory.
- It resets for each top-level engine entry.
- If an async continuation resumes on another thread, the per-thread allocation baseline
  cannot enforce the same limit.
- Parsing, host callbacks, module payloads, output serialization, and allocations on other
  threads are not a hard part of this budget.
- A managed limit cannot guarantee that the process avoids `OutOfMemoryException`.

**Required host action.** Configure conservative Jint limits, input/module/output limits,
and an operating-system memory limit. Treat Jint's memory constraint as defense in depth,
not a heap quota.

### TM-07: Stack exhaustion terminates the process

**Threat.** Recursive or deeply nested execution can exhaust the native .NET stack.
`StackOverflowException` is not a recoverable request error and can terminate the worker.

**Existing mitigations.**

- `LimitRecursion` limits JavaScript recursion depth when configured.
- `Constraints.StackOverflowGuard` probes the remaining native stack on every interpreted
  function entry and converts exhaustion to a catchable `RangeError`.
- The older `Constraints.MaxExecutionStackCount` lane can move call-expression recursion
  to a fresh thread.

**Missing or residual mitigation.**

- All stack protections are disabled by default.
- `LimitRecursion` counts repeated function definitions rather than every possible function
  entry shape.
- `MaxExecutionStackCount` only covers call expressions and takes precedence over the more
  complete stack overflow guard.
- A timeout alone is not a reliable stack-overflow defense.

**Required host action.** Enable `StackOverflowGuard` and configure a tested recursion
limit. Do not select the older `MaxExecutionStackCount` lane for untrusted code unless its
partial coverage is intentional. Keep process isolation so a runtime stack failure cannot
kill unrelated server workloads.

### TM-08: Blocking host calls and `Atomics.wait` evade timely cancellation

**Threat.** A projected delegate, property getter, converter, synchronous module loader, or
CLR method can block indefinitely. Constraint checks occur after ordinary interop calls
return; they cannot interrupt the call. `Atomics.wait` can block the engine thread with an
infinite timeout and `AgentCanSuspend` defaults to `true`.

**Existing mitigations.**

- Time and cancellation constraints detect an overrun after many interop calls return.
- `AgentCanSuspend = false` disables `Atomics.wait`.
- `MaxAtomicsPauseIterations` caps `Atomics.pause` spin work.
- Async host and module APIs avoid holding a thread while waiting for I/O.

**Missing or residual mitigation.**

- There is no safe in-process mechanism to abort a blocked .NET call or thread.
- Timing out Jint does not necessarily stop an underlying host Task or external operation.

**Required host action.** Set `AgentCanSuspend = false`. Do not expose blocking APIs. Make
host operations asynchronous, bounded, and cancellation-aware. Kill the isolated worker
when the outer deadline expires.

### TM-09: A sequence of engine entries escapes a per-entry budget

**Threat.** `Execute`, `Evaluate`, `Invoke`, and `Call` are separate top-level runs. Built-in
constraints reset around each one, so a host loop that invokes a script once per record
grants a fresh timeout, statement budget, and allocation baseline to every call.

**Existing mitigations.**

- Nested re-entry does not reset the outer execution's constraints.
- `OperationDeadlineConstraint` can span a host-defined multi-entry operation.

**Missing or residual mitigation.**

- Jint cannot infer which entries form one server request.
- `OperationDeadlineConstraint` is inert unless the host explicitly brackets the operation.

**Required host action.** Arm one operation deadline around all engine work for the request,
including module import, callbacks, and result handling. Keep per-entry constraints as an
additional ceiling.

### TM-10: Async and promise work outlives the request

**Threat.** A script can create pending promises, fire-and-forget host Tasks, asynchronous
module loads, and queued jobs. Work may retain request objects, consume external resources,
or attempt to resume after the request has ended.

**Existing mitigations.**

- Promise and module waits have a configurable `PromiseTimeout`.
- Async completions are queued so background threads do not execute JavaScript directly.
- Event-loop generations discard promise/module completions from a cycle ended by
  `RestoreGlobalSnapshot`.
- Async module loading can receive the registered cancellation constraint's token.

**Missing or residual mitigation.**

- The cancellation-token parameter on `EvaluateAsync`, `ExecuteAsync`, and `InvokeAsync`
  only controls promise settlement waiting; it does not preempt the initial synchronous
  interpreter run.
- Discarding a completion does not cancel the underlying Task, I/O, timer, or host action.
- A promise timeout is not a total request budget.

**Required host action.** Register an engine cancellation constraint in addition to passing
the async API token. Track and cancel all host operations, await intended work before
disposing the request scope, and terminate the worker on an outer timeout.

### TM-11: Module loaders expose files, networks, and secrets

**Threat.** Module specifiers and import graphs are attacker-controlled. A filesystem loader
can disclose files; a custom HTTP loader can become an SSRF primitive, follow redirects,
download unbounded content, or reach metadata/internal services. A large or cyclic graph
can consume resources. Loader exception messages, source names, module locations, stack
traces, and `import.meta.url` can disclose paths, hostnames, queries, or credentials.

**Existing mitigations.**

- Modules and `require` are disabled by default.
- `DefaultModuleLoader` restricts resolution to its base URI by default.
- The default loader has no package or HTTP support.
- Module loads are cached per engine and async loads can be coalesced.

**Missing or residual mitigation.**

- Custom loader policy is entirely host-defined.
- Jint has no universal scheme, origin, redirect, DNS, response-byte, graph-size, or graph-
  depth policy.
- Base-path checking is not a replacement for filesystem permissions or an OS sandbox.
- Async loader failures expose the supplied exception message to script.

**Required host action.** Prefer host-registered modules. Otherwise use explicit scheme,
origin, resolved-IP, path, redirect, size, count, depth, and timeout allow-lists. Use a
credential-free module identity, sanitize loader errors, and apply restrictive filesystem
and network policy to the worker.

### TM-12: Cross-request state leakage and prototype pollution

**Threat.** Reusing an engine lets one script leave globals, lexical bindings, mutated
intrinsics/prototypes, registered modules, `Symbol.for` entries, host object mutations,
closures, event-loop work, or engine-affine values for a later request. Prototype pollution
can alter or observe subsequent execution.

**Existing mitigations.**

- A fresh `Engine` provides a fresh realm.
- `RestoreGlobalSnapshot` restores global bindings, resets selected transient state, and
  fences old promise/module completions.
- Prepared scripts and modules are safe to share across engines.

**Missing or residual mitigation.**

- `RestoreGlobalSnapshot` explicitly does not revert intrinsic/prototype mutations, nested
  host object graphs, CLR state, registered modules, `Symbol.for`, or
  `Advanced.HostDefined`.
- A constraint exception can leave partially mutated state.
- A host-retained JavaScript function or object remains engine-affine and can carry an old
  request's closures and authority.

**Required host action.** Use a fresh engine per request for mutually distrusting users, or
at minimum per tenant/trust domain. Share `Prepared<T>`, not `JsValue` or `Module`. Convert
results to bounded CLR data before discarding the engine. Do not use snapshots as a
security boundary.

### TM-13: Concurrent or wrong-thread engine use corrupts isolation

**Threat.** `Engine` is not thread-safe. Concurrent requests using one engine can race
globals, execution contexts, constraints, callbacks, and result state. This can cause
incorrect authorization context, cross-request disclosure, crashes, or hangs.

**Existing mitigations.**

- Async completion paths queue work to the event loop and guard which waiter drains it.
- Documentation states that an engine is single-threaded.

**Missing or residual mitigation.**

- General concurrent use is not serialized or rejected automatically.
- Event-loop thread-safety does not make the engine safe for concurrent host calls.

**Required host action.** Give an engine exclusive ownership for its entire lifetime, or
serialize the complete execution and async drain with one gate. Never return a pooled engine
while asynchronous work is outstanding.

### TM-14: Regular expression denial of service

**Threat.** Adversarial patterns and subjects can cause excessive backtracking and CPU use.

**Existing mitigations.**

- Regex execution has a 10-second timeout by default on both supported regex paths.
- `RegexTimeoutInterval` can lower the timeout.

**Missing or residual mitigation.**

- Ten seconds per regex is usually too high for a server request.
- Repeated regex operations can consume the entire request budget.
- A regex timeout does not replace the overall execution deadline.

**Required host action.** Set a short, workload-tested regex timeout and retain the overall
time, statement, and process CPU limits.

### TM-15: Debugger features bypass production assumptions

**Threat.** Debug callbacks expose scopes and values, script `debugger` statements may pause
execution depending on configuration, and debugger expression evaluation intentionally
skips amortized timeout/cancellation checks while paused.

**Existing mitigations.**

- Debug mode is disabled and `debugger` statements are ignored by default.

**Missing or residual mitigation.**

- A host that exposes debugger controls to an untrusted user creates a separate privileged
  evaluation surface.

**Required host action.** Do not enable debugger functionality or CLR debugger breaks in the
untrusted production path. Treat remote debugging as privileged administrative access.

### TM-16: Error, source, and logging disclosure

**Threat.** Script-visible errors, host exception messages, module locations, source names,
stack traces, and logs can reveal filesystem paths, internal hosts, credentials, CLR type
signatures, or other tenants' data. Attacker-controlled text can forge log lines or produce
very large diagnostics.

**Existing mitigations.**

- Detailed CLR resolution errors and CLR inner-exception chaining are disabled by default.
- `GetJavaScriptErrorString()` omits a chained CLR exception.
- CLR exceptions are not catchable by script unless the host opts in.
- Function source text retention is disabled by default.

**Missing or residual mitigation.**

- Loader error messages and host-provided source/module names can still be script-visible.
- Host logging and HTTP response formatting are outside Jint.

**Required host action.** Use opaque source and module identifiers, never put credentials in
URLs, sanitize host errors, cap diagnostic size, encode log fields structurally, and return
generic server errors while retaining sensitive details only in protected telemetry.

### TM-17: Result conversion and serialization denial of service

**Threat.** The script can return a huge, cyclic, proxy-backed, getter-heavy, or deeply
nested graph. Converting it to CLR data, enumerating it, serializing it, or logging it may
run additional code and consume CPU or memory outside the intended execution budget.

**Existing mitigations.**

- The host controls whether and how values cross out of the engine.
- `JSON.parse` has a configurable maximum parse depth.

**Missing or residual mitigation.**

- Jint does not impose a universal response byte, object depth, property count, or host
  serializer limit.
- Returning an engine-owned `JsValue` extends the engine and realm lifetime.

**Required host action.** Define a small result schema, copy only approved primitive fields,
cap depth/property count/bytes, serialize under the outer request budget, reject cycles, and
discard engine-owned values with the engine.

### TM-18: Retention and cache-based memory growth

**Threat.** Engine pools, snapshots, prepared code, wrapper identity, resolved reflection
accessors, warmed call sites, module registries, and host-held values can retain engines,
types, closures, request objects, or large graphs longer than expected.

**Existing mitigations.**

- Recent object-wrapper caching is bounded.
- Full wrapper identity tracking is opt-in.
- Prepared AST state is engine-neutral; engine-affine handler caches live on the engine.
- Snapshots document that they strongly retain their engine and captured values.

**Missing or residual mitigation.**

- Several caches intentionally live for the engine or process and do not evict.
- A pooled engine can retain the last receiver or closure seen at warmed sites.
- Host callbacks, `HostDefined`, and returned values can retain request scopes.

**Required host action.** Bound engine lifetime, avoid full identity tracking for unbounded
object sets, use private `TypeResolver` instances for collectible types, clear request state,
and monitor heap/caches. Prefer fresh engines when request graphs are sensitive.

### TM-19: Shared mutable values become covert channels

**Threat.** Reusing the same mutable host object, request service, shared buffer, or
engine-owned value across users lets one script communicate with or alter another user's
execution. `Symbol.for` also persists within an engine.

**Existing mitigations.**

- Separate engines have separate realms and module registries.
- Most engine-affine advanced APIs reject objects from another engine.

**Missing or residual mitigation.**

- Sharing a `JsValue` across engines is unsupported and not validated on every path.
- Separate engines do not isolate host objects that the application deliberately shares.

**Required host action.** Never share engine-owned objects between engines. Do not project
mutable singletons into mutually distrusting engines. Copy data at the boundary and isolate
host-side caches by tenant.

### TM-20: Dependency and runtime vulnerabilities

**Threat.** A parser, interpreter, regex engine, .NET runtime, or Jint defect may turn
malformed input into a crash, denial of service, information leak, or sandbox escape.

**Existing mitigations.**

- Jint uses an interpreter rather than emitting user-controlled IL.
- The project runs unit and Test262 conformance suites and accepts private vulnerability
  reports.

**Missing or residual mitigation.**

- In-process code cannot contain a vulnerability in itself or its runtime.
- Jint has no mechanism to patch the host's runtime or dependencies.

**Required host action.** Track supported Jint and .NET releases, apply security updates,
scan dependencies, fuzz the application-specific integration, and retain process isolation
and least privilege as defense in depth.

## Hardened deployment baseline

The numbers below are examples only. Measure normal workloads and choose smaller limits that
leave acceptable headroom.

```csharp
var deadline = new OperationDeadlineConstraint();

var engine = new Engine(options =>
{
    options.Strict();
    options.DisableStringCompilation();

    options.TimeoutInterval(TimeSpan.FromSeconds(2));
    options.MaxStatements(50_000);
    options.LimitMemory(16_000_000);
    options.LimitRecursion(64);
    options.MaxArraySize(100_000);
    options.RegexTimeoutInterval(TimeSpan.FromMilliseconds(250));
    options.CancellationToken(requestAborted);
    options.Constraint(deadline);

    options.Constraints.StackOverflowGuard = true;
    options.Constraints.PromiseTimeout = TimeSpan.FromSeconds(2);

    options.AgentCanSuspend = false;
    options.AllowClrWrite(false);
    options.Interop.ArrayConversion = ArrayConversionMode.Copy;

    // Do not call AllowClr(). Leave modules and the debugger disabled.
});

deadline.Begin(TimeSpan.FromSeconds(3), requestAborted);
try
{
    return engine.Evaluate(boundedSource).ToObject();
}
finally
{
    deadline.End();
}
```

This configuration is incomplete without host controls:

1. Reject oversized script and module source before parsing.
2. Run one request in a disposable, least-privileged worker with OS CPU and memory quotas.
3. Deny filesystem and outbound network access unless explicitly required.
4. Expose only narrow, authorized, cancellation-aware host functions.
5. Use a fresh engine for mutually distrusting requests.
6. Cap module graphs, callback work, returned object shape, serialized bytes, logs, and
   total request time.
7. Discard the worker if it misses the outer deadline; do not wait indefinitely for
   in-process cleanup.

## Verification checklist

Before deploying a host that executes untrusted scripts:

- [ ] CLR namespace access, `AllowGetType`, reflection, debugger, and `Atomics.wait` are off.
- [ ] Projected host objects are immutable or intentionally capability-scoped and read-only.
- [ ] Source, module graph, callback, result, and log limits are enforced outside Jint.
- [ ] Time, statement, memory, recursion, stack, array, regex, promise, and operation limits
  are explicitly configured and tested with adversarial inputs.
- [ ] Async API cancellation is paired with an engine cancellation constraint.
- [ ] No engine, mutable host object, `JsValue`, module, or request context crosses trust
  domains.
- [ ] Custom module loaders enforce scheme, origin, IP, path, redirect, size, and timeout
  policies and disclose no secrets in names or errors.
- [ ] Worker identity, filesystem, network, CPU, memory, and lifetime are restricted outside
  Jint.
- [ ] Timeout, cancellation, memory, stack, loader, and serialization failures are exercised
  under production-like concurrency.
- [ ] Jint, Acornima, dependencies, and the .NET runtime are monitored for security updates.

## Reporting vulnerabilities

Do not open a public issue for a suspected sandbox escape, constraint bypass, process crash,
or unintended host access. Follow the private reporting instructions in
[SECURITY.md](SECURITY.md).
