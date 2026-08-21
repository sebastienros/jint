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

`Options.ForUntrustedCode(UntrustedCodeLimits)` is the opt-in hardened profile. It leaves
the defaults unchanged for compatibility, but closes the static capabilities below and
requires the host to supply finite request-appropriate resource limits.

| Control | Default | Security effect |
| --- | --- | --- |
| CLR namespace access (`Interop.Enabled`) | Disabled | `System`, `importNamespace`, and `clrHelper` are not installed |
| `AllowGetType`, `AllowSystemReflection` | Disabled | Blocks instance type discovery, type widening, and reflection namespace/object access |
| `AllowedAssemblies` | Empty until `AllowClr` adds entries | Closed allow-list for `System` / `importNamespace` type discovery, not for what admitted APIs can do |
| Writes through projected CLR objects | Disabled | Fields, properties, indexers, dictionary/list entries, and live array elements require explicit `AllowClrWrite()` opt-in |
| CLR array conversion | Copy | Script receives a detached native JavaScript array snapshot |
| Modules | Disabled | The fail-fast loader rejects imports |
| Module graph limits | Unlimited | Count, source bytes, graph depth, and resolution hops are unbounded unless configured |
| Module load policy | None | A custom loader's resolved targets are unrestricted unless a policy is configured |
| `require` | Disabled | No CommonJS-like loader global |
| Web platform APIs (`Options.WebApi.Features`) | `None` | No `console`, timers, `URL`, `Blob`, … globals exist |
| `fetch` | Disabled, and never part of `WebApiFeatures.Default` | Only `UseFetch()` grants script outbound HTTP |
| Timeout, statement, memory, recursion limits | None | Untrusted execution is unbounded unless the host opts in |
| Parser source-length and AST-node limits | None | Hostile source and parser output are unbounded unless the host opts in |
| Stack overflow guard | Enabled | Interpreted function entry checks remaining native stack and turns exhaustion into a catchable `RangeError` |
| Maximum array size | `uint.MaxValue` | Effectively unbounded for hostile input |
| Regex timeout | 10 seconds | Bounds an individual regular expression operation |
| Promise wait timeout | 10 seconds | Bounds host APIs that wait for promise or module settlement |
| Concurrent `Engine` use | Rejected | Public host entries throw instead of racing or silently serializing |
| Dynamic `eval` / `Function` compilation | Enabled | Script can create and parse additional source at runtime |
| `Atomics.wait` suspension | Disabled | A default engine rejects synchronous waits with `TypeError`; `Atomics.waitAsync` remains available |
| Debugger | Disabled | Debug callbacks and debugger expressions are inactive |
| Detailed CLR resolution errors | Disabled | Reduces script-visible CLR surface disclosure |
| Detailed caught CLR exception messages | Disabled | Replaces host exception text with a generic script-visible error |
| Detailed module load errors | Disabled | Hides loader messages, canonical paths, URLs, and parse source names from script |
| Function source retention | Disabled | Submitted function source is not retained solely for `toString()` |
| Result conversion and JSON output limits | Unlimited | Compatibility default; hostile output is unbounded unless the host opts in |

## Configuration diagnostics

`Options.ValidateSecurityConfiguration(SecurityConfigurationPolicy.UntrustedScripts)` inspects the
configured `Options` without invoking deferred callbacks or constraint factories. It returns an immutable report whose diagnostics
have stable `JINTSECnnn` codes, an error or warning severity, a description, and remediation
guidance. Results are sorted by code so deployment logs and policy tests are deterministic.
`EnsureSecurityConfiguration` performs the same validation and throws
`SecurityConfigurationException` when the report contains an error; ordinary engine construction
does not validate or change defaults automatically.

The untrusted-script policy reports:

- missing per-entry and cumulative operation deadlines, statement, memory, cancellation, recursion,
  native-stack, parser, result, and array limits;
- non-positive and saturated statement, memory, and timeout values that remove a limit;
- blocking `Atomics.wait`, dynamic string compilation, CLR access, reflection, `GetType`, CLR
  writes, and live CLR array views;
- module loaders, aggregate count/byte limits, per-load depth/hop limits, destination policies,
  `require`, debugger behavior, and detailed CLR/module errors that require host review;
- unbounded or long regex and synchronous promise waits; and
- directly registered `Constraint` instances whose mutable state would be shared by engines built
  from one `Options` object; and
- host callbacks and converters, CLR compatibility access, live array/write combinations, decorators,
  retained source, and callbacks registered to run during engine construction.

Explicit parsing options and prepared programs are separate configuration surfaces:
`ValidateSecurityConfiguration(options, parsingOptions)` checks an override together with the
engine options, while `ValidateSecurityConfiguration` / `EnsureSecurityConfiguration` on
`ScriptPreparationOptions` and `ModulePreparationOptions` check the regex timeout that will be
embedded in prepared code. The actual source-length, AST-node, source-retention, and regex settings
are validated together; the default preparation options therefore report unbounded parser limits and
the 10-second regex warning.

After construction, `engine.Advanced.ValidateSecurityConfiguration()` reads the effective options and
the constraint instances the engine already created. It does not replay a callback or factory. Public
`Configure`, `SetTypeConverter`, and `UseHostFactory` callbacks continue to produce `JINTSEC031`;
Jint-owned configuration has separate internal provenance so a future first-party hardened profile can
compose without suppressing user callbacks or relying on delegate names.

This is a configuration check, not a capability proof or hardened-profile implementation.
In particular, it cannot inspect the behavior of a projected delegate or object, prove that a
custom `TypeResolver` is a complete allow-list, determine whether a module loader blocks SSRF or
path traversal, infer whether a cancellation token has an outer deadline, or enforce process,
transport redirects, callback behavior, external serialization, response, or log limits. A warning is
not an approval: it means the setting can be legitimate only after the host verifies and tests the
corresponding external policy. A clean report still requires the worker isolation and host controls
in this document. Treat the validated `Options` as immutable and pass it directly to
`Engine(Options)` and inspect the effective engine report when deferred configuration exists. Zero
findings do not prove sandbox safety.

## Threat inventory

### TM-01: CLR access becomes process authority

**Threat.** Enabling CLR interop lets script reach powerful .NET APIs. A type admitted by
the namespace allow-list, explicitly exported as a `TypeReference`, or returned through a
projected host object carries the authority of its exposed constructors and members. The
result can be filesystem or environment access, network access, process creation, native
interop, or arbitrary code execution with the worker's identity.

**Existing mitigations.**

- CLR namespace access, `AllowGetType`, and `AllowSystemReflection` are disabled by default.
- `AllowedAssemblies` is a closed allow-list for namespace discovery, including nested and
  generic type definitions. Namespace lookup admits only effectively public types: top-level
  types must be public, and every declaring type in a nested chain must be public. `AllowClr()`
  without arguments adds only the core assembly that contains `System.Object`; supplying
  assemblies adds only those assemblies.
- `TypeResolver.MemberFilter` filters namespace-discovered types, nested types, constructors,
  and projected members.
- `AllowGetType` does not expose the static `System.Type.GetType(string)` family. Its
  `clrHelper` type-widening operations also enforce the namespace type policy.
- `AllowSystemReflection` gates namespace discovery and wrapping for `System.Reflection`.
- `ForUntrustedCode` resets all three controls and removes configured assemblies.
- `ExposeDetailedResolutionErrors` is disabled by default.

**Missing or residual mitigation.**

- Jint cannot reduce the operating-system authority of an enabled CLR API.
- `AllowedAssemblies` governs namespace discovery only. Host-projected objects, delegates,
  and explicitly exported `TypeReference` values are separate capabilities and can expose
  types from assemblies absent from that list.
- A type admitted from an allowed assembly may itself load assemblies, resolve or instantiate
  types by name, or return powerful objects. Assembly membership alone is therefore not a
  complete capability boundary; a positive member allow-list is still required.
- A member filter is host code and can be incomplete or become stale as types evolve.
- Enabling `AllowGetType` still grants runtime type discovery for otherwise admitted objects.
- Filtering one well-known static type-resolution family is defense in depth, not a general
  barrier: other admitted APIs may carry equivalent type-resolution or invocation authority.
- Enabling `AllowSystemReflection`, or explicitly exporting reflection capabilities, restores
  the authority those APIs carry.

**Required host action.** Prefer leaving CLR namespace access disabled for untrusted scripts.
If it is unavoidable, pass the minimum explicit assembly set to `AllowClr`, use a dedicated
allow-listing `TypeResolver`, leave `AllowGetType` and `AllowSystemReflection` disabled, and
test the complete exported capability surface. Treat every projected object, delegate, and
explicit `TypeReference` as outside the assembly boundary, and audit every admitted member
for authority it can return or exercise. Still isolate the worker at the operating-system level.

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
- `ForUntrustedCode` removes registered CLR extension methods as well as disabling CLR
  namespace access.

**Missing or residual mitigation.**

- `Interop.Enabled = false` does not make projected CLR objects inert.
- Jint cannot infer authorization, request scope, safe URLs, query cost, or output bounds.
- User converters and factories are host code and are not automatically interruptible.

**Required host action.** Expose purpose-built, least-authority functions or immutable data,
not service providers or general domain objects. Re-authorize every operation inside the
callback, validate all arguments, cap call frequency and result size, propagate
cancellation, and avoid returning powerful objects.

### TM-03: Script mutates host state

**Threat.** Direct writes through an `ObjectWrapper` are disabled by default, and CLR arrays
cross as detached copies by default. A host can explicitly enable writes with
`AllowClrWrite()` and independently select `ArrayConversionMode.LiveView`; doing both lets
script alter host properties, fields, indexers, dictionaries, lists, live arrays, or objects
reachable from them. Calling an exposed instance, static, or extension method can mutate host
state regardless of the direct-write option.

**Existing mitigations.**

- Direct writes through CLR wrappers are disabled by default; `AllowClrWrite()` is an
  explicit opt-in.
- `ArrayConversionMode.Copy` is the default and disconnects the JavaScript array container
  from later CLR mutations and script writes.
- Immutable DTOs and Jint-owned record layouts avoid live write-through.
- `ForUntrustedCode` selects both read-only wrappers and copied CLR arrays.

**Missing or residual mitigation.**

- Read-only wrapper configuration does not make a mutable object graph immutable outside
  Jint.
- Calling exposed methods or extension methods can still mutate state even when direct
  writes are disabled.
- A copied CLR array is only a shallow projection boundary: reference-type elements can still
  expose mutable host objects.
- `ArrayConversionMode.LiveView` restores live reads and avoids the initial O(N) copy and
  allocation, but does not grant write authority; `AllowClrWrite()` is additionally required
  for write-through.
- Wrapper caches can reuse a copied snapshot, preserving script-side mutations and identity
  across crossings, but they never make later CLR-side array mutations visible through it.

**Required host action.** Leave CLR writes disabled and keep `ArrayConversionMode.Copy`
explicitly pinned in hardened configurations. Expose immutable snapshots whose elements are
also safe to project. Use `LiveView` only when live observation is intentional, and enable CLR
writes only when shared mutation is a separately authorized capability. Do not expose
mutating methods unless they are intentional, authorized capabilities.

### TM-04: Infinite loops and computational denial of service

**Threat.** Loops, generators, proxies, coercion hooks, large collection operations,
BigInt/string arithmetic, and adversarial algorithms can monopolize a worker.

**Existing mitigations.**

- `TimeoutInterval`, `MaxStatements`, and `CancellationToken` constraints.
- Long-running built-ins periodically call the constraint set.
- Amortized timeout and cancellation checks are also made after CLR calls return.
- The untrusted-script configuration report identifies missing limits and sentinel values that
  remove them.
- `ForUntrustedCode` requires finite time and statement budgets.

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
- `ForUntrustedCode` disables dynamic string compilation.
- Function source text retention is disabled by default.
- `Options.Parsing.MaxSourceLength` bounds UTF-16 parser input across initial execution,
  dynamic compilation, ShadowRealm evaluation, debugger evaluation, and JavaScript or JSON
  module source. Generated source-offset padding and function-constructor wrappers count.
- `Options.Parsing.MaxNodeCount` stops parsing when Acornima completes more than the
  configured number of AST nodes.
- Per-call parsing options can tighten engine limits, static preparation supports the same
  limits, and a breach throws the non-script-catchable `ParsingLimitException`.
- Asynchronous module completion carries both the parser limits captured when the load was
  registered and the originating memory-operation state across its thread hop. A fatal
  parsing breach removes the pending load and releases async ownership rather than becoming
  a script-catchable rejection.
- The default file loader reads at most the configured source length plus one UTF-16 code
  unit before rejecting, rather than materializing the complete oversized source string.
- `MaxTotalModuleSourceBytes` can reject loader-provided source before Jint parses it.

**Missing or residual mitigation.**

- Initial `Execute(string)` and `Evaluate(string)` parse before
  `ExecuteWithConstraints`; execution timeout, statement, and memory constraints do not
  bound that parse; parser limits are separate and opt-in.
- The parser source limit counts decoded UTF-16 code units, not transport bytes. The module
  graph byte limit checks loader-provided bytes before decoding, but a custom loader may
  already have buffered an oversized response before handing its contents to Jint.
- The node limit bounds the completed AST size, not every speculative parser operation or a
  wall-clock CPU budget. Dynamic parsing cannot be preempted while Acornima is between node
  completions.
- Prepared scripts and modules are not rechecked when executed. The host that prepares or
  accepts a prepared AST is responsible for applying preparation limits at that trust
  boundary. Those limits do continue to govern dynamic source compiled while it executes.
- Prepared modules and custom module records have no original encoded source length and
  therefore contribute zero to `MaxTotalModuleSourceBytes`.
- Parser limits do not cap aggregate imported source, module count, graph depth, or network
  policy.

**Required host action.** Configure parser source and node limits plus module count, byte,
depth, and hop limits. Cap encoded request/module bytes, source offsets, and custom-loader
transport responses before Jint, and parse in the disposable worker. Disable string
compilation unless it is a requirement.

### TM-06: Memory exhaustion

**Threat.** Script can allocate strings, arrays, typed arrays, object graphs, closures,
promises, modules, symbols, and host wrappers until the process is out of memory or spends
most of its time in garbage collection.

**Existing mitigations.**

- `LimitMemory` measures managed allocations only while an engine thread is actively
  executing an operation. It includes synchronous host callbacks and carries the accumulated
  budget with promise reactions, `EvaluateAsync` / `InvokeAsync`, and asynchronous module
  loading when they resume on another thread.
- Per-thread execution segments avoid charging unrelated process allocations while an
  asynchronous operation is suspended. A missing runtime allocation counter is reported by
  `MemoryLimitConstraint.Accuracy` and fails execution explicitly instead of silently
  disabling enforcement.
- `MemoryLimitConstraint.Begin` / `End` can apply one allocation budget across a host
  operation made from several top-level engine entries.
- The engine ownership guard also covers the memory scope's mutable and diagnostic surfaces;
  another thread, or a host call made while an async API is outstanding, fails before it can
  reset, disarm, or inspect the in-flight budget.
- `MaxArraySize` bounds Jint array creation when configured.
- `ForUntrustedCode` requires finite memory and array limits.
- Some built-ins reject impossible allocations.
- Recent-wrapper caching is bounded by default; the unbounded identity map is opt-in.
- The untrusted-script configuration report identifies missing memory and practical array limits.

**Missing or residual mitigation.**

- No memory or practical array limit is configured by default.
- The memory constraint measures managed allocations during engine execution segments, not
  retained heap, unmanaged memory, or process memory.
- It resets for each top-level engine entry unless the host explicitly brackets a multi-entry
  operation with `Begin` / `End`.
- Initial source parsing, work performed by asynchronous producers before they return a
  result, worker threads started by host callbacks, module payload storage outside engine
  turns, and output serialization are not part of this budget.
- A managed limit cannot guarantee that the process avoids `OutOfMemoryException`.

**Required host action.** Configure conservative Jint limits, input/module/output limits,
verify `MemoryLimitConstraint.Accuracy`, use `Begin` / `End` when one request drives several
engine entries, await any asynchronous entry before calling `End`, and enforce an
operating-system memory limit. Treat Jint's memory constraint as defense in depth, not a heap
quota.

### TM-07: Stack exhaustion terminates the process

**Threat.** Recursive or deeply nested execution can exhaust the native .NET stack.
`StackOverflowException` is not a recoverable request error and can terminate the worker.

**Existing mitigations.**

- `LimitRecursion` limits JavaScript recursion depth when configured.
- `Constraints.StackOverflowGuard` probes the remaining native stack on every interpreted
  function entry and converts exhaustion to a catchable `RangeError`.
- `ForUntrustedCode` requires a finite recursion limit and enables `StackOverflowGuard`.
- The older `Constraints.MaxExecutionStackCount` lane can move call-expression recursion
  to a fresh thread.
- The untrusted-script configuration report identifies disabled, saturated, or shadowed stack
  protections.

**Missing or residual mitigation.**

- A host can explicitly disable `StackOverflowGuard`; doing so restores the native stack-exhaustion
  process-termination risk.
- `LimitRecursion` counts repeated function definitions rather than every possible function
  entry shape.
- `MaxExecutionStackCount` only covers call expressions and takes precedence over the more
  complete stack overflow guard.
- The guard covers entry into interpreted functions, not arbitrary native stack consumption in
  host callbacks or the CLR.
- A timeout alone is not a reliable stack-overflow defense.

**Required host action.** Leave `StackOverflowGuard` enabled (or set it explicitly in a hardened
profile) and configure a tested recursion limit. Do not select the older
`MaxExecutionStackCount` lane for untrusted code unless its partial coverage is intentional.
Keep process isolation so a runtime or host stack failure cannot kill unrelated server workloads.

### TM-08: Blocking host calls and `Atomics.wait` evade timely cancellation

**Threat.** A projected delegate, property getter, converter, synchronous module loader, or
CLR method can block indefinitely. Constraint checks occur after ordinary interop calls
return; they cannot interrupt the call. `Atomics.wait` can block the engine thread with an
infinite timeout when a worker-like host explicitly opts in with `AgentCanSuspend = true`.

**Existing mitigations.**

- Time and cancellation constraints detect an overrun after many interop calls return.
- `AgentCanSuspend` defaults to `false`; `Atomics.wait` throws `TypeError` before registering
  a waiter unless the host opts in.
- `Atomics.waitAsync` remains available without blocking the engine thread.
- `ForUntrustedCode` sets `AgentCanSuspend = false`.
- `MaxAtomicsPauseIterations` caps `Atomics.pause` spin work.
- Async host and module APIs avoid holding a thread while waiting for I/O.

**Missing or residual mitigation.**

- Setting `AgentCanSuspend = true` restores the previous behavior, including an indefinite
  wait when script omits the timeout.
- There is no safe in-process mechanism to abort a blocked .NET call or thread.
- Timing out Jint does not necessarily stop an underlying host Task or external operation.

**Required host action.** Keep `AgentCanSuspend = false` for untrusted scripts and prefer
`Atomics.waitAsync`. A worker-like host that requires synchronous waits must opt in explicitly
and accept that only termination of its isolated worker can preempt an indefinite wait. Do
not expose blocking APIs. Make host operations asynchronous, bounded, and cancellation-aware.
Kill the isolated worker when the outer deadline expires.

### TM-09: A sequence of engine entries escapes a per-entry budget

**Threat.** `Execute`, `Evaluate`, `Invoke`, `Call`, `Advanced.ConvertResult`, public
`JsonSerializer.Serialize` calls, and bounded
`JavaScriptException.GetJavaScriptErrorString` are separate top-level runs. Built-in
constraints reset around each one, so a host loop that invokes a script once per record
grants a fresh timeout, statement budget, and allocation baseline to every call. Evaluating
a result and then converting or serializing it also grants each phase a fresh budget unless
the host brackets both with one operation deadline.

**Existing mitigations.**

- Nested re-entry does not reset the outer execution's constraints.
- `OperationDeadlineConstraint` can span a host-defined multi-entry operation.
- `ForUntrustedCode` requires a finite operation duration, installs one deadline per engine,
  and exposes `UntrustedCodeLimits.BeginOperation` to arm it for a scope.
- The profile rejects overlapping operation scopes on one engine rather than letting an inner
  scope disarm the outer deadline.

**Missing or residual mitigation.**

- Jint cannot infer which entries form one server request.
- `OperationDeadlineConstraint` is inert unless the host explicitly brackets the operation.
- The deadline is cooperative. An idle asynchronous wait may not reach another constraint
  checkpoint until its separate promise timeout or cancellation token wakes it.

**Required host action.** Arm one `OperationDeadlineConstraint` around all engine work for the
request, including module import, callbacks, `ConvertResult`, `JsonSerializer`, and bounded error
rendering. When evaluation and conversion or serialization form one request, they must be inside
the same `UntrustedCodeLimits.BeginOperation` scope. Keep per-entry constraints as an additional
ceiling.

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
- `MaxModuleCount`, `MaxTotalModuleSourceBytes`, `MaxModuleGraphDepth`, and
  `MaxModuleResolutionHops` can bound a configured graph. Limit failures propagate like
  execution constraints rather than becoming catchable import rejections.
- `IModuleLoadPolicy` can inspect every final resolved target. `ModuleAllowlistPolicy`
  provides composable scheme, exact-host, exact-origin, canonical-file-root, and bare-
  specifier controls.
- Loader exceptions and module parse locations are redacted from script by default. The
  original exception remains available to the host through
  `JintException.TryGetClrException`.

**Missing or residual mitigation.**

- All graph limits and policy are opt-in.
- The built-in policy sees the final `ResolvedSpecifier`, not redirects, DNS answers, or
  transport response headers inside a custom loader. Redirect count, every redirect target,
  resolved IP ranges, transport bytes, and timeouts remain loader responsibilities.
- Prepared modules, exports-only modules, and custom module records whose encoded source
  size is unknowable contribute zero to the source-byte limit.
- Graph depth and resolution hops are per top-level load. A script can stage a larger
  engine-lifetime graph across multiple dynamic imports or by loading dependencies first;
  `MaxModuleCount` and the cumulative source-byte limit remain the aggregate bounds.
- File-root policy is lexical and does not resolve symbolic links or Windows reparse points;
  an allowed root containing attacker-controlled links can still escape the lexical root.
- Base-path checking is not a replacement for filesystem permissions or an OS sandbox.
- A loader can deliberately bypass automatic redaction with
  `ModuleLoadCompletion.SetError(string)`, an error decorator, or an explicit
  `JavaScriptException` message.
- `Module.Location`, `import.meta`, debugger/source metadata, and successful module values
  are not anonymized. The host still controls those names and values.
- `ExposeDetailedErrors()` or `Modules.ExposeDetailedLoadErrors = true` restores detailed
  loader and source messages for trusted development environments.

**Required host action.** Prefer host-registered modules. Otherwise configure all four graph
limits and an `IModuleLoadPolicy`; also enforce resolved-IP, redirect, transport-size, and
timeout policy inside the loader. Use a credential-free module identity, report failures
with `SetError(Exception)`, leave detailed load errors disabled, and apply restrictive
filesystem and network policy to the worker.

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

- Public engine, module, constraint, and advanced-operation entries claim exclusive ownership
  and fail fast with `InvalidOperationException` when another thread or outstanding async host
  operation owns the engine.
- Same-thread synchronous callback re-entry is allowed. Async entry from inside an active
  engine call is rejected before work starts; top-level async APIs reserve ownership for the
  lifetime of their returned `Task` and transfer the active thread when a continuation resumes.
- JavaScript callbacks converted to CLR delegates carry operation-scoped authorization. A host
  may dispatch one to another thread while its CLR call or async operation is outstanding; Jint
  yields and transfers the reserved engine one callback turn at a time without admitting unrelated
  public callers.
- Background Task and module completions only enqueue work; an owning host turn drains it.

**Missing or residual mitigation.**

- The guard covers Jint's public host entry points, not direct calls on engine-owned objects
  already handed to the host, such as mutating `engine.Global` or a retained `ObjectInstance`.
- Fail-fast detection prevents concurrent state corruption; it does not make one engine a
  tenant-isolation boundary or repair host state mutated before an exception.
- Authorized callback transfers may wait for the preceding callback turn to release ownership.
  The fail-fast guarantee applies to unrelated public host entries, not this explicitly serialized
  continuation of the operation that already owns the engine.
- A host can still share projected CLR objects across otherwise separate engines.

**Required host action.** Give an engine exclusive ownership for each complete operation and
treat a concurrency exception as an integration bug, not a retry signal. Await every async
engine call before returning a pooled engine, and do not mutate retained engine-owned objects
from another thread. Use separate engines for mutually distrusting requests.

### TM-14: Regular expression denial of service

**Threat.** Adversarial patterns and subjects can cause excessive backtracking and CPU use.

**Existing mitigations.**

- Regex execution has a 10-second timeout by default on both supported regex paths.
- `RegexTimeoutInterval` can lower the timeout.
- `ForUntrustedCode` requires an explicit finite regex timeout.

**Missing or residual mitigation.**

- Ten seconds per regex is usually too high for a server request.
- Repeated regex operations can consume the entire request budget.
- A regex timeout does not replace the overall execution deadline.
- Explicit parsing and preparation options override the engine timeout; prepared programs retain
  that timeout when shared across engines.

**Required host action.** Set a short, workload-tested regex timeout and retain the overall
time, statement, and process CPU limits. Validate every explicit parsing/preparation configuration
and set the nested preparation `ParsingOptions.RegexTimeout` for untrusted source.

### TM-15: Debugger features bypass production assumptions

**Threat.** Debug callbacks expose scopes and values, script `debugger` statements may pause
execution depending on configuration, and debugger expression evaluation intentionally
skips amortized timeout/cancellation checks while paused.

**Existing mitigations.**

- Debug mode is disabled and `debugger` statements are ignored by default.
- `ForUntrustedCode` resets debug mode, statement handling, and initial stepping to disabled
  values if earlier configuration enabled them.

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

- Caught CLR exception messages, CLR method/constructor resolution details, module loader
  failures, and module parse locations are redacted from script by default.
- Original CLR and loader exceptions remain available host-side through
  `JintException.TryGetClrException`; resolution type/member metadata remains available
  through `TryGetClrType` and `TryGetClrMemberName`.
- CLR inner-exception chaining is disabled by default.
- `GetJavaScriptErrorString()` omits a chained CLR exception.
- CLR exceptions are not catchable by script unless the host opts in.
- Function source text retention is disabled by default.

**Missing or residual mitigation.**

- Explicit messages passed to `JavaScriptException` or
  `ModuleLoadCompletion.SetError(string)`, decorator-added properties, successful module
  values, and host-provided source/module names can still be script-visible.
- `ExposeDetailedErrors()`, `Interop.ExposeDetailedExceptionMessages`,
  `Interop.ExposeDetailedResolutionErrors`, `Modules.ExposeDetailedLoadErrors`, and
  `ChainClrExceptions()` are development/diagnostic opt-ins that can expose host details
  through their documented channels.
- Host logging and HTTP response formatting are outside Jint.

**Required host action.** Use opaque source and module identifiers, never put credentials in
URLs, leave detailed error options and CLR exception chaining disabled, use exception-based
module failures rather than explicit strings, cap diagnostic size, encode log fields
structurally, and return generic server errors while retaining sensitive details only in
protected telemetry.

### TM-17: Result conversion and serialization denial of service

**Threat.** The script can return a huge, cyclic, proxy-backed, getter-heavy, or deeply
nested graph. Converting it to CLR data, enumerating it, serializing it, or logging it may
run additional code and consume CPU or memory outside the intended execution budget.

**Existing mitigations.**

- The host controls whether and how values cross out of the engine.
- `JSON.parse` has a configurable maximum parse depth.
- `ResultLimits` can bound conversion/serialization depth, cumulative properties or elements,
  individual strings, aggregate characters, and UTF-8 or binary bytes.
- `Advanced.ConvertResult` copies Jint-owned arrays, typed arrays, maps, sets, and enumerable
  object properties to a detached CLR graph, rejects cycles, and enforces the selected limits
  before known-size output allocations and before property getters are read.
- Jint's JSON serializer enforces the same limits while walking, counts escaped characters
  before appending, and checks exact UTF-8 bytes before touching a writer.
- Conversion, JSON serialization, and bounded JavaScript error rendering run under execution
  constraints because getters, proxy traps, `toJSON`, replacers, and `stack` accessors can run
  script.

**Missing or residual mitigation.**

- Result limits are unlimited by default.
- Shared references that are not cycles are converted once per occurrence and can amplify the
  detached CLR graph. `MaxPropertyCount` is the structural-work and container-allocation bound;
  string, character, and binary-byte limits do not substitute for it.
- Proxy `ownKeys` and host property-key hooks can allocate their key list before Jint receives
  and counts it; memory constraints and process isolation remain the backstop.
- Observable coercion hooks on boxed strings and numbers must run before Jint knows the
  resulting primitive's size; they can allocate before the result limiter can inspect it.
- A CLR wrapper target is already host-owned and is returned without walking its object graph.
- `System.Text.Json`, Newtonsoft.Json, configured `Interop.SerializeToJson` delegates, custom
  converters, logging formatters, standard `Exception.ToString()`, and debugger frontends run
  outside Jint's bounded walker. Jint can cap a delegate's returned text before copying it, but
  cannot undo work already performed inside the delegate.
- Returning an engine-owned `JsValue` extends the engine and realm lifetime.

**Required host action.** Define a small result schema, configure and tune `ResultLimits` (the
`Conservative` preset is only a starting point), and return `Advanced.ConvertResult` output or
bounded `JsonSerializer` output. Keep time, statement, cancellation, memory, stack, worker, and
response-server byte limits around the whole operation. Independently configure every external
serializer and log sink, and discard engine-owned values with the engine.

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

### TM-21: `fetch` turns script into a client of the host's network position

**Threat.** Enabling `WebApiFeatures.Fetch` gives script outbound HTTP with the worker's own
network identity. Whatever the process can reach, script can reach: an internal service, a
database admin port, a service mesh sidecar, a cloud instance-metadata endpoint. A redirect
is part of the attack surface, not just of the transport — a reachable server the script is
allowed to call can answer `302 Location: http://169.254.169.254/latest/meta-data/` and
launder the request past a first-hop check. A response is attacker-influenced in size: a
compression bomb or an endless stream buffers into the worker's heap. Concurrency is
attacker-controlled too, so a loop of `fetch` calls holds sockets, connections, and
buffers without bound. URLs, headers, and bodies the script composes can carry the host's
credentials to a destination the script chose, and header values it controls could splice a
second request into the connection.

This is the same class of threat as [TM-11](#tm-11-module-loaders-expose-files-networks-and-secrets),
which covers the loader-shaped version of it. The difference is that a module loader is
host-written and `fetch` is not: the policy has to live in Jint, because there is no host
code between the script and the socket.

**Existing mitigations.**

- The feature is **off by default and not part of `WebApiFeatures.Default`**. `UseWebApis()`
  never enables it; `UseFetch()` is the only call that does.
- `Options.WebApi.Fetch.AllowedSchemes` defaults to `https` and `http`, checked before a
  socket is opened.
- `Options.WebApi.Fetch.UrlFilter` is the host's allow-list, and is **re-run on every
  redirect hop**: Jint drives redirects itself with `AllowAutoRedirect` off, so no hop
  escapes the check.
- `Options.WebApi.Fetch.MaxRedirects` (20) bounds the hop count.
- `Options.WebApi.Fetch.MaxResponseBytes` (32 MiB) bounds the **decompressed** body, checked
  against the declared `Content-Length` and again as a running total, so a lying or chunked
  response is caught too.
- `Options.WebApi.Fetch.Timeout` (30 s) is enforced CLR-side on the request's cancellation
  token, so it fires even for an engine nobody is pumping.
- `Options.WebApi.Fetch.MaxConcurrentRequests` (10) bounds in-flight requests per engine, and
  the excess is refused rather than queued.
- `Authorization`, `Cookie`, and `Proxy-Authorization` are stripped when a redirect crosses
  origin; a `303`, and a `301`/`302` on a `POST`, drop the body and its content headers.
- A URL carrying credentials (`https://user:pass@host/`) is refused by the `Request`
  constructor.
- Header names must be RFC 9110 tokens and header values may not contain NUL, CR, or LF, so
  script cannot splice a second request into the connection.
- Every network-class failure is one `TypeError` whose message is only `Failed to fetch`; the
  originating CLR exception rides the error *value* and is readable by the host through
  `JintException.TryGetClrException`, never by script.
- The default client has `UseCookies = false`, so no cookie jar is shared between engines or
  tenants.
- A request in flight is cancelled by `Engine.Advanced.RestoreGlobalSnapshot`, and its promise
  never settles into the restored engine.
- Timer flooding is covered separately by `Options.WebApi.Timers.MaxActiveTimers` (1000), and
  timers only fire while the host pumps the engine.

**Missing or residual mitigation.**

- `UrlFilter` sees a URL, not a resolved IP. It cannot by itself stop DNS rebinding or a
  hostname that resolves to a link-local or private address.
- There is no per-request or per-engine byte, connection, or bandwidth budget beyond the
  per-response cap and the concurrency cap; a script may still issue many bounded requests in
  sequence.
- `credentials`, `cache`, `mode`, `referrer`, and `integrity` are accepted and ignored, so a
  script cannot rely on them for protection and neither can a host reading the request.
- The default shared `HttpClient` is process-wide and never disposed; its connection pool is
  shared by every engine that did not supply a client.
- Response bodies are buffered in memory, so `MaxResponseBytes` is also the per-request heap
  cost.

**Required host action.** Do not enable `UseFetch` for untrusted script unless the deployment
needs it. When it is needed, set a `UrlFilter` that allow-lists destinations rather than
denying known-bad ones, drop `http` from `AllowedSchemes`, lower `MaxResponseBytes`,
`MaxRedirects`, `Timeout`, and `MaxConcurrentRequests` to what the workload actually needs,
and supply an `HttpClient` (or `HttpClientFactory`) whose handler applies the deployment's
proxy, DNS, and TLS policy. Resolve-and-check the destination address in the handler if
rebinding is in scope. Keep the worker's egress restricted at the network layer as well:
`UrlFilter` is a policy inside the process, not a firewall.

### TM-22: `EventSource` holds that network position open

**Threat.** `WebApiFeatures.EventSource` is a second, separate grant of the egress
[TM-21](#tm-21-fetch-turns-script-into-a-client-of-the-hosts-network-position) describes, and
the destination reachability question is identical — including the redirect-laundering one.
What differs is duration and persistence. A connection is **long-lived by design**, so a
script can pin sockets, connections, and server-side resources for as long as the engine
lives rather than for one exchange. The response is a **stream**, so no total-size cap can
apply to it. And the protocol **reconnects by itself**: a stream that ends, or fails at the
network layer, comes back on a delay the *server* chooses through its `retry:` field, so an
attacker-controlled endpoint also controls how often the host retries it.

**Existing mitigations.**

- The feature is **off by default and not part of `WebApiFeatures.Default`**. `UseWebApis()`
  never enables it, and — unlike every other implication in the feature set — **`UseFetch()`
  does not either**, nor does enabling this one enable `fetch`.
- Destination policy is the same object and the same code as fetch's:
  `Options.WebApi.Fetch.AllowedSchemes`, `UrlFilter` (re-run on every redirect hop **and on
  every reconnection**, so revoking a destination between two attempts stops the next one) and
  `MaxRedirects`.
- `Options.WebApi.Fetch.MaxResponseBytes` is repurposed as the **maximum size of one event**
  — the data buffer plus the line being read — which is what the parser has to hold; exceeding
  it fails the connection.
- `Options.WebApi.Fetch.MaxConcurrentRequests` bounds the streams one engine may have open,
  counted separately from the fetches in flight.
- A refused URL, a non-`200` response, a `Content-Type` that is not `text/event-stream`, and a
  blown per-event cap all **fail the connection for good**: `readyState` becomes `CLOSED` and
  nothing retries, so none of them can become a retry loop.
- The reconnect delay is an entry on the engine's timer queue, so it counts against
  `Options.WebApi.Timers.MaxActiveTimers` and **fires only while the host pumps the engine**.
  A connection likewise delivers nothing to an engine nobody pumps.
- `close()` cancels the request in flight, and so does
  `Engine.Advanced.RestoreGlobalSnapshot`, which additionally drops the pending reconnection
  and delivers nothing from the ended cycle into the restored engine. An engine cancellation
  ends the connection silently rather than becoming an `error` event a script could react to.
- The event a script receives carries no detail about the failure — the standard gives an
  event source's `error` event none — so failures cannot be read apart to map the network.

**Missing or residual mitigation.**

- **`Options.WebApi.Fetch.Timeout` deliberately does not apply**, because an idle connection
  is what the protocol is for. There is therefore no wall-clock bound on how long one stream
  may stay open; `close()`, a restore, or dropping the engine is what ends it.
- There is no floor on the server-announced `retry:` value and no exponential backoff, so a
  hostile endpoint that ends every stream immediately with `retry: 0` makes the engine
  reconnect as fast as the host pumps it. The concurrency cap bounds how many such streams
  exist, not how often each retries.
- No cap on total bytes or on event count over the life of a connection: the per-event cap
  bounds memory, not throughput.
- `withCredentials` is accepted and ignored, so neither a script nor a host may rely on it.
- The residuals of TM-21 that are about destinations rather than duration — DNS rebinding, the
  shared process-wide `HttpClient` — apply here unchanged.

**Required host action.** Treat it as you would `UseFetch`: allow-list destinations with
`UrlFilter`, drop `http` from `AllowedSchemes`, and lower `MaxResponseBytes` and
`MaxConcurrentRequests`. Additionally, because there is no deadline: bound the *lifetime* of
the engine itself for untrusted script — one worker per request, discarded when the request
ends — rather than pooling an engine that a script may leave streaming.

### TM-23: The Cache API gives script storage that outlives the evaluation

**Threat.** `WebApiFeatures.CacheApi` is the first feature whose whole point is that data a
script writes is still there later — for a longer-lived engine, and for anything else the
host wires to the same `CacheStorageProvider`. Three consequences follow. A script can
**consume storage**: the entries it `put`s live wherever the provider keeps them, on the
host's disk or database if that is what the provider is. A script can **poison what a later
reader trusts**: a stored `Response` replays to whoever `match`es it, so two tenants sharing
one provider, or a host reading a cache a script could write, turn a cache entry into an
injection channel. And with `UseFetch` also granted, `cache.add` records what the *network*
said — a cache filled through a redirect-day DNS answer replays it long after the DNS has
moved on.

**Existing mitigations.**

- The feature is **off by default and not part of `WebApiFeatures.Default`** — like `Storage`,
  and for the same reason: a host asking for "the web APIs" has not agreed to durable storage.
  Enabling it brings the fetch *object model*, never the network; `cache.add`/`addAll` reject
  with a `TypeError` until `UseFetch` is also granted, and with it they run under fetch's full
  policy (scheme list, `UrlFilter`, size cap, deadline) exactly as a scripted `fetch` would.
- The provider seam is **host-supplied and engine-free**: entries cross it as plain CLR
  records, everything runs on the engine's thread, and a provider's
  `CacheQuotaExceededException` surfaces as the standard `QuotaExceededError`, so quotas,
  eviction and per-tenant partitioning have a sanctioned place to live.
- The defaulted provider is **private to one engine** — nothing is shared between engines, and
  nothing survives the engine being dropped.

**Missing or residual mitigation.**

- **The default `InMemoryCacheStorageProvider` has no quota.** A script can `put` until the
  process runs out of memory; nothing in the engine bounds it. It also survives
  `RestoreGlobalSnapshot` — a restore reverts global bindings, not host storage — so a pooled
  engine carries one cycle's cache into the next unless the host swaps or clears the provider
  itself.
- Nothing validates what a provider returns against what was stored; a provider shared across
  trust boundaries is itself the boundary, and partitioning is entirely its job.

**Required host action.** For untrusted script, supply the provider: enforce a byte quota and
an entry cap in it (throwing `CacheQuotaExceededException` when exceeded), partition it per
tenant, and treat cached responses read outside the engine as script-controlled input. With a
pooled engine, swap or clear the provider per request the same way `HostDefined` is swapped.
If the default in-memory store is used at all, bound the engine's lifetime instead — the
store dies with the engine, which is the only bound it has.

### TM-24: `WebSocket` is a bidirectional, peer-driven hold on the network

**Threat.** `WebApiFeatures.WebSocket` is the third separate egress grant, and the
destination question is
[TM-21](#tm-21-fetch-turns-script-into-a-client-of-the-hosts-network-position)'s unchanged.
What it adds over [TM-22](#tm-22-eventsource-holds-that-network-position-open) is
**direction**: the socket sends as well as receives, so an admitted destination is not just
readable but a full duplex channel a script can exfiltrate through, at whatever rate the host
pumps. Like an event stream it is **long-lived by design** — the peer can keep it open
indefinitely and there is no deadline to end it — and its liveness is **peer-driven**: what
arrives, and how often, is the other end's choice.

**Existing mitigations.**

- **Off by default, not in `WebApiFeatures.Default`**, and permission-independent from
  `fetch` and `EventSource`: enabling any one of the three enables none of the others. They
  share their *settings*, not their permission.
- Destination policy is fetch's own `Options.WebApi.Fetch` group, with the scheme list read
  in its WebSocket sense (`http` admits `ws`, `https` admits `wss`, or name `ws`/`wss`
  outright). The `UrlFilter` is shown the `ws:` URL the script asked for, which fails safe: a
  filter written for fetch that tests `uri.Scheme == "https"` refuses every socket rather
  than admitting one it was never shown.
- **No redirects exist to launder through**: the WHATWG handshake sets redirect mode to
  `error`, so the one URL the filter admitted is the only one the socket can reach —
  TM-21's redirect-laundering residual does not apply here.
- `Options.WebApi.Fetch.Timeout` bounds the **opening handshake**, so a peer that never
  completes it cannot pin a pending connection forever.
- `Options.WebApi.Fetch.MaxResponseBytes` bounds **one message** in either direction; an
  incoming message over the cap fails the connection with close code 1009 rather than
  buffering without bound.
- `Options.WebApi.Fetch.MaxConcurrentRequests` bounds the sockets one engine may have open,
  counted separately from fetches and event streams; the constructor refuses the socket over
  the limit.
- Events dispatch only from the engine's job queue **while the host pumps**; the realm and
  event-loop generation are captured at construction, so `RestoreGlobalSnapshot` closes the
  socket and delivers nothing from the ended cycle. An engine cancellation erupting through a
  handler stays a constraint rather than becoming an `error` event a script could swallow.

**Missing or residual mitigation.**

- **No deadline on an open socket** — `Timeout` covers the handshake only. `close()`, a
  restore, or dropping the engine is what ends one.
- **No throughput cap**: the per-message cap bounds memory, not bytes-over-lifetime, in
  either direction — nothing bounds what a script sends to an admitted destination.
- No automatic reconnection exists (unlike TM-22), so there is no retry loop to bound — but
  script can trivially write its own in an `onclose` handler, bounded only by the concurrency
  cap and the host's pumping.
- The residuals of TM-21 that are about destinations — DNS rebinding, the shared
  process-wide `HttpClient`'s connection reuse — apply unchanged.

**Required host action.** As for `UseFetch`: allow-list destinations with `UrlFilter`
(remember it sees `ws:`/`wss:` schemes), drop `http` from `AllowedSchemes`, and lower
`MaxResponseBytes` and `MaxConcurrentRequests`. Because the channel is bidirectional, admit
only destinations you would let the script *write* to, not merely read. And as with TM-22,
bound the engine's own lifetime for untrusted script rather than pooling an engine a script
may leave connected.

### TM-25: Web Crypto work factors are attacker-chosen and uninterruptible

**Threat.** Three `crypto.subtle` operations take their cost from a number the script writes
down. `generateKey` for RSA is a prime search whose cost grows far faster than the modulus
does. `deriveBits` for PBKDF2 is a loop whose only purpose is to be slow and whose trip count
is the `iterations` member. `deriveBits` for HKDF expands to a length the caller asks for.
Each of them is **one BCL call**, so the whole of the work happens between two interpreter
statements: `TimeoutInterval`, `MaxStatements`, `LimitMemory` and a `CancellationToken` are
checkpoints *around* it and none of them can interrupt it. `iterations: 2 ** 40` is one line
of script that would otherwise run for days.

This is [TM-04](#tm-04-infinite-loops-and-computational-denial-of-service)'s computational
denial of service arriving in
[TM-08](#tm-08-blocking-host-calls-and-atomicswait-evade-timely-cancellation)'s
uninterruptible-call shape, with one difference that makes it sharper than either: no host
wrote the call and no host can decline to expose it, because — unlike `fetch`, `EventSource`,
`WebSocket`, `Storage` and the Cache API — `WebApiFeatures.Crypto` **is** part of
`WebApiFeatures.Default`. A host that asked for "the web APIs" already has all three work
factors.

**Existing mitigations.**

- Each of the three has a ceiling this engine imposes, checked *before* the call is made and
  reported as an `OperationError` that names the restriction rather than pretending the request
  was invalid:
  - **RSA key generation** — `RsaAlgorithm.MaxGeneratedModulusLength` = 8192 bits. The platform
    itself goes to 16384, which is minutes of CPU inside one synchronous operation; 8192 is
    above every key size in use and bounds a generation to seconds. It is checked ahead of the
    specification's own parameter validation, so the arithmetic that validation performs on
    2^*modulusLength* is bounded before it is performed.
  - **PBKDF2 iterations** — `Pbkdf2Algorithm.MaxIterations` = 2^22 = 4,194,304, which is above
    every OWASP 2023 recommendation for the function (1,300,000 for SHA-1, 600,000 for SHA-256,
    210,000 for SHA-512) and bounds one call to roughly 1.7 seconds at the ceiling itself —
    measured 0.35 s for SHA-256, 1.5 s for SHA-1 and 1.7 s for SHA-512.
  - **HKDF expansion** — `255 * hashLen`, which is RFC 5869's own limit rather than one this
    engine invented (the expansion counter `T(1) … T(N)` is a single octet) and is the
    specification's step 3. `HkdfAlgorithm.DeriveBits` checks it here rather than leaving it to
    `HKDF.DeriveKey`, whose refusal is an `ArgumentOutOfRangeException` that would erupt out of a
    promise-returning operation.
- The ceilings are the engine's, so they hold whatever the platform's provider would have
  accepted; the platform's own `LegalKeySizes` is then consulted as well
  (`RsaAlgorithm.RequireGeneratableModulusLength`).
- `crypto.getRandomValues` carries the standard's 65,536-byte quota
  (`CryptoInstance.MaxRandomBytes`), so the generator cannot be asked for an unbounded draw in
  one call.
- A key's `[[handle]]` is DER rather than a live `RSA`, so no operating-system key handle is
  tied to a garbage-collection schedule and a script cannot accumulate native key material.

**Missing or residual mitigation.**

- **The ceilings are fixed constants, not options.** A host can neither lower nor raise them, so
  an untrusted script may always spend one 8192-bit generation or one 2^22-iteration derivation
  per call — and may make such calls in a loop. A ceiling bounds one call and never a sequence,
  which is [TM-09](#tm-09-a-sequence-of-engine-entries-escapes-a-per-entry-budget)'s shape
  reappearing inside a single run.
- Nothing bounds how many crypto operations one script performs, and a wall-clock constraint
  notices the overrun only once the call it was armed against has returned.
- `sign`, `verify`, `encrypt`, `decrypt`, `digest` and `wrapKey` carry no work factor: their
  cost scales with the length of their input, so what bounds them is what bounds the script's
  allocations — `LimitMemory` and `MaxArraySize` — and each is still one uninterruptible call
  over a buffer whose size the script chose.
- Key *import* is a parse, and its cost belongs to the platform's ASN.1 or JWK reader rather
  than to anything this engine schedules.

**Required host action.** Budget for crypto as CPU the request has to pay for, not as a
constraint the engine will enforce: size the outer, process-level deadline so that it survives
at least one ceiling-cost operation, since neither `TimeoutInterval` nor an
`OperationDeadlineConstraint` can preempt one. Where a script has no business generating keys or
stretching passwords, drop the feature —
`options.UseWebApis(WebApiFeatures.Default & ~WebApiFeatures.Crypto)` — rather than relying on
the ceilings. Keep the worker's operating-system CPU limit as the only mechanism that can
actually stop a call in progress.

### TM-26: `FetchEvents` lets script claim the host's inbound requests

**Threat.** `WebApiFeatures.FetchEvents` runs the opposite direction to
[TM-21](#tm-21-fetch-turns-script-into-a-client-of-the-hosts-network-position): where `fetch`
lets a script reach *out* to the network, this routes requests the host already holds *in* to
the script. With it enabled, one line — `addEventListener('fetch', e => e.respondWith(…))` —
makes the evaluated script the answer to every request the host passes to
`Engine.Advanced.InvokeFetchHandler`. A script submitted to compute a value can instead decide
what the server replies with, and read everything the request carries on the way. The
registration leaves no trace in the script's own result, so a host that only inspects the
returned value never sees it happen.

**Existing mitigations.**

- **Off by default and never part of `WebApiFeatures.Default`**, which the enum states as a
  standing promise alongside `Fetch` and `Storage`.
- **Disjoint from `Fetch` in both directions.** `WebApiRegistration.ExpandFeatures` closes this
  flag over `GlobalEvents`, `Url` and `Files` and pointedly not over `Fetch`, so naming it
  grants no outbound network access and `UseFetch()` grants no inbound routing. Enabling it
  installs `Headers`, `Request` and `Response` — a listener that cannot build a `Response` has
  nothing to respond with — and not `fetch`, the same split
  `Engine.Advanced.SetFetchHandler` already makes.
- **A handler the host registered outranks every listener.** `RequireFetchRoute` reads the
  `SetFetchHandler` slot first and returns it outright; listeners are consulted only where there
  was nothing to win against, so script cannot take a route away from the host by adding a line.
  `Engine.Advanced.HasFetchHandler` reports that registration alone and deliberately answers
  `false` for an engine whose script registered a listener, so script cannot change the host's
  own record of what the host did either.
- **An unanswered dispatch fails the operation.** When no listener called `respondWith()` —
  because none did, or because the ones that ran threw — `DispatchFetchEvent` raises, and
  `InvokeFetchHandler` turns that into the operation's failure. There is no fall-through to a
  network an embedded engine does not have, and no empty success response.
- **Only the engine's own event can be responded to.** `respondWith()` and `waitUntil()` run the
  Service Workers Standard's first step — an untrusted event cannot be extended
  (`JsFetchEvent.AddLifetimePromise`) — so a script that constructs its own `FetchEvent` and
  dispatches it cannot manufacture a response, and the host's operation can only ever settle
  from the event `FetchEventConstructor.CreateTrustedFetchEvent` made for a real inbound request.
- **One constraint bracket covers the whole dispatch.** `DispatchFetchEvent` wraps
  `listeners.DispatchEvent` in a single `Engine.ExecuteWithConstraints` — which is literally what
  `Engine.Call` does for the handler route — so however many listeners run, they share one
  `MaxStatements` allowance and one armed timeout instead of each being handed a fresh budget.
- A constraint failure is a `JintException` that is not a `JavaScriptException`, so it erupts
  past a `DiagnosticsSink` and becomes the operation's failure rather than a promise that never
  settles.
- `RestoreGlobalSnapshot` drops the synthetic global listener target
  (`WebApiEngineState.ResetTransientState`), so listeners registered in one cycle cannot answer
  the next cycle's requests on a pooled engine.

**Missing or residual mitigation.**

- **A listener sees the request as the host routed it in — that is the feature, not a leak.**
  The URL, method, headers and the fully buffered body are all readable by script, so anything
  the host would not show the script must not be in the `HttpRequestMessage` it hands to
  `InvokeFetchHandler`.
- Listener registration is script state, not host state: it comes and goes with the evaluation
  cycle, a script may add one after the host looked, and one that exists may still decline to
  respond. `HasFetchHandler` is therefore not a way to ask whether the engine can serve a
  request.
- The value the operation settles from is a script-built `Response` — status, headers and body
  are all the script's, and validating them on the way out is the host's job.
- The bracket bounds the dispatch, not the turns the host pumps afterwards: an `async` listener's
  continuations are separate entries with budgets of their own
  ([TM-09](#tm-09-a-sequence-of-engine-entries-escapes-a-per-entry-budget)).
- With a `DiagnosticsSink` set, a listener that responds and *then* throws still serves its
  response; with no sink the same script fails the operation. The two configurations disagree
  deliberately, because with nowhere to report to, preferring the response would lose the
  exception entirely.

**Required host action.** Do not enable it for untrusted script: a host that wants a script to
handle requests should name the handler itself with `SetFetchHandler`, which is resolved once,
at a point the host chose, and which script cannot displace. Where the feature is enabled,
strip the request down to what the script may see before routing it, treat everything on the
returned `Response` as untrusted input, bracket the whole request — the invocation plus every
turn pumped after it — in an `OperationDeadlineConstraint`, and use a fresh engine per request
so that one script's listener cannot answer the next script's traffic.

### TM-27: A shared `BroadcastChannelBroker` is a channel between engines

**Threat.** `Options.MessagingOptions.Broker` is the one setting whose purpose is to join two
engines: `BroadcastChannel` objects on engines sharing a broker hear each other by name, so
scripts the host runs in what it treats as separate sandboxes can signal one another, coordinate,
and pass data. This is
[TM-19](#tm-19-shared-mutable-values-become-covert-channels)'s shared-mutable-state threat
arriving as a configuration option rather than as a leaked object, and it is reached without any
host object being projected at all — a channel name is an arbitrary attacker-chosen string, so
two scripts that know the same name are in contact. A subscription is also a strong reference
chain from the broker to the engine, so a broker outliving an engine keeps that engine, its
realm and everything its listeners closed over reachable.

**Existing mitigations.**

- **The default is private.** A `null` `Broker` gives each engine a broker of its own
  (`WebApiEngineState.BroadcastChannels`), so channels on one engine hear each other and nothing
  crosses an engine boundary unless the host deliberately shared one. It is defaulted lazily, so
  an engine that never creates a channel allocates nothing.
- **The broker is read once, when the engine is built** (`WebApiEngineState.AttachMessaging`), so
  assigning one to shared `Options` afterwards does not reach an engine that already exists.
- **No `JsValue` crosses.** `JsBroadcastChannel.PostMessage` serializes on the sending engine's
  thread into a `SerializationRecord` that belongs to no engine, and only that record is
  enqueued. Deserialization, the `MessageEvent` and the listeners all run on whichever thread
  pumps the *receiving* engine, so an engine nobody pumps never takes delivery.
- **Serialize once, copy per destination.** One broadcast produces one record and every
  destination deserializes that same record, which makes it the one thing in the engine
  deserialized more than once — so `JsBroadcastChannel.Receive` constructs its
  `StructuredDeserializer` with `sharedRecord: true` and the byte storage is **copied** rather
  than adopted (`StructuredDeserializer.DeserializeArrayBuffer`). Without that, two receivers
  would come away with two `ArrayBuffer`s over one `byte[]` and each could see the other's
  writes — a cross-engine aliasing channel underneath the serialization boundary.
- **There is no transfer list**, so no `ArrayBuffer` is ever detached by a broadcast, and an
  uncloneable value is a synchronous `DataCloneError` at the `postMessage` call.
- **Three paths release a subscription**, each unsubscribing from the broker so a finished engine
  stops being reachable from it: `close()` from script, `RestoreGlobalSnapshot` (whose
  `WebApiEngineState.ResetTransientState` calls `CloseBroadcastChannels`), and `Engine.Dispose`
  (whose `WebApiEngineState.Dispose` calls the same method).
- **A channel belongs to the evaluation cycle it was created in.**
  `BroadcastChannelSubscription` captures `Engine.EventLoopGeneration` at construction rather
  than reading the receiver's current one from the sender's thread, and a delivery both
  early-outs on the sender's side and is fenced at dequeue on the receiver's — so a message
  cannot run a dead cycle's listeners against restored globals.
- A channel name whose last subscriber leaves is removed from the broker's map outright, so a
  script cycling through names cannot grow it.

**Missing or residual mitigation.**

- **Engines that share a broker trust each other with volume: no quota exists.** Nothing bounds
  message size, message rate, how many channels one engine opens, or how deep a queue a message
  builds on a receiver that is pumped slowly or not at all. One script can fill another engine's
  event loop, and the receiving engine spends its own constraint budget deserializing and
  dispatching what arrived.
- Retention is strong and there is no finalizer-driven cleanup and none planned: a host sharing
  one broker between long-lived and short-lived engines must dispose or restore the short-lived
  ones, or the broker keeps every one of them alive.
- One broker is one agent cluster and one origin — exactly as much separation as the host's
  decision to share it leaves. The broker enforces no per-tenant partitioning, and a channel name
  is not a secret.
- The broker is deliberately **not** reset by `RestoreGlobalSnapshot`: what a restore ends is the
  channels, not the cluster they were in, so a pooled engine rejoins the same broker in the next
  cycle.

**Required host action.** Share a broker only within one trust domain, and give mutually
distrusting scripts the private default by saying nothing. Where one is shared, treat every
message delivered on a channel as untrusted input, apply the receiving engine's time, statement
and memory constraints to the turns that deliver it, and dispose or restore any engine that
leaves the cluster. Do not treat a channel name as a capability, and do not use a broker as a
control plane for anything the scripts on the other side are not allowed to trigger.

## Hardened deployment baseline

`ForUntrustedCode` configures the in-engine controls in one place and rejects the sentinel
values that ordinary constraint helpers interpret as unlimited. The numbers below are
examples only. Measure normal workloads and choose smaller limits that leave acceptable
headroom.

```csharp
var limits = new UntrustedCodeLimits(
    timeoutInterval: TimeSpan.FromSeconds(2),
    maxStatements: 50_000,
    memoryLimit: 16_000_000,
    maxRecursionDepth: 64,
    maxArraySize: 100_000,
    regexTimeout: TimeSpan.FromMilliseconds(250),
    promiseTimeout: TimeSpan.FromMilliseconds(500),
    maxOperationDuration: TimeSpan.FromSeconds(3),
    maxSourceLength: 100_000,
    maxNodeCount: 25_000,
    maxModuleCount: 50,
    maxTotalModuleSourceBytes: 1_000_000,
    maxModuleGraphDepth: 10,
    maxModuleResolutionHops: 200,
    resultLimits: new ResultLimits(
        maxDepth: 16,
        maxPropertyCount: 10_000,
        maxStringLength: 100_000,
        maxOutputCharacters: 1_000_000,
        maxOutputBytes: 2_000_000));

// This declaration is safe to share across concurrent Engine construction.
var sharedOptions = new Options().Strict().ForUntrustedCode(limits);
using var engine = new Engine(sharedOptions);
using (limits.BeginOperation(engine, requestAborted))
{
    try
    {
        var value = engine.Evaluate(boundedSource);
        return new JsonSerializer(engine).Serialize(value).AsString();
    }
    catch (JavaScriptException exception)
    {
        return exception.GetJavaScriptErrorString(limits.ResultLimits);
    }
}
```

`ForUntrustedCode` deliberately disables modules. A host that must execute untrusted modules
cannot use that closed profile unchanged; it needs a separately reviewed, hand-built configuration
with explicit limits and an allowlist matched to the loader:

```csharp
options.EnableModules(moduleRoot);
options.Modules.MaxModuleCount = 50;
options.Modules.MaxTotalModuleSourceBytes = 1_000_000;
options.Modules.MaxModuleGraphDepth = 10;
options.Modules.MaxModuleResolutionHops = 200;
options.Modules.LoadPolicy = new ModuleAllowlistPolicy
{
    AllowedSchemes = { "file" },
    AllowedFileRoots = { moduleRoot },
};
options.EnsureSecurityConfiguration(SecurityConfigurationPolicy.UntrustedScripts);
```

A network loader must additionally enforce redirect targets/count, DNS and resolved-IP
policy, response bytes, and timeout/cancellation inside the loader. Jint cannot observe
those transport steps.

The operation scope is required: only the host knows which engine entries make up one request.
It carries a cumulative deadline, cancellation token, and managed-allocation budget across
evaluation/import and bounded output handling; statement limits keep their documented per-entry
reset. It cannot preempt a host callback that does not return or bound retained/native memory and
operating-system resources, so the outer worker deadline remains the hard stop. Prepared scripts
and modules must use matching parsing limits. Property writes are blocked, but deliberately
projected methods and delegates remain host capabilities.

This configuration is incomplete without host controls:

1. Reject oversized encoded script and module input before decoding, and configure Jint's
   parser source-length and AST-node limits.
2. Run one request in a disposable, least-privileged worker with OS CPU and memory quotas.
3. Deny filesystem and outbound network access unless explicitly required.
4. Expose only narrow, authorized, cancellation-aware host functions.
5. Use a fresh engine for mutually distrusting requests.
6. Tune `ResultLimits`, and separately cap module graphs, callback work, external serializers,
   HTTP response bytes, logs, and total request time.
7. Discard the worker if it misses the outer deadline; do not wait indefinitely for
   in-process cleanup.

## Verification checklist

Before deploying a host that executes untrusted scripts:

- [ ] The fully configured `Options` object is validated before engine construction; every
  reported warning is explicitly reviewed and the report is regression-tested by stable code.
- [ ] CLR namespace access, `AllowGetType`, reflection, debugger, and `Atomics.wait` are off.
- [ ] Direct CLR writes remain disabled, and projected host objects are immutable or intentionally
  capability-scoped with no unintended mutating instance, static, or extension methods.
- [ ] CLR arrays use `Copy`; any `LiveView` opt-in is intentional, remains read-only unless
  write-through is separately authorized, and does not expose mutable element objects accidentally.
- [ ] Initial source, callback, external serializer, HTTP response, and log limits are enforced
  outside Jint; `ResultLimits` is configured for Jint-owned conversion and JSON output, and all
  four Jint module graph limits are configured when modules are enabled.
- [ ] Time, statement, memory, recursion, stack, array, regex, promise, and operation limits
  are explicitly configured and tested with adversarial inputs.
- [ ] Every explicit parsing option and script/module preparation option is validated with the
  timeout that will actually be used.
- [ ] Every multi-entry request is enclosed in `UntrustedCodeLimits.BeginOperation`.
- [ ] Every operation scope receives the request's cancellable token, and async work is awaited
  before the scope is disposed or the engine is returned to a pool.
- [ ] No engine, mutable host object, `JsValue`, module, or request context crosses trust
  domains.
- [ ] An `IModuleLoadPolicy` restricts final resolved targets, and custom module loaders
  enforce scheme, origin, redirect, resolved-IP, path, transport-size, and timeout policies
  and disclose no secrets in names or errors.
- [ ] `fetch` is off, or its `UrlFilter`, `AllowedSchemes`, `MaxResponseBytes`,
  `MaxRedirects`, `Timeout`, and `MaxConcurrentRequests` allow-list the destinations and the
  budgets the workload actually needs, and worker egress is restricted at the network layer
  as well.
- [ ] Worker identity, filesystem, network, CPU, memory, and lifetime are restricted outside
  Jint.
- [ ] Timeout, cancellation, memory, stack, loader, and serialization failures are exercised
  under production-like concurrency.
- [ ] Jint, Acornima, dependencies, and the .NET runtime are monitored for security updates.

## Reporting vulnerabilities

Do not open a public issue for a suspected sandbox escape, constraint bypass, process crash,
or unintended host access. Follow the private reporting instructions in
[SECURITY.md](SECURITY.md).
