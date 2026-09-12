# Generated bindings and debugger integration: X6 review drafts

These are repository review drafts for [campaign #3575, X6](https://github.com/sebastienros/jint/issues/3575),
prepared under [#3904](https://github.com/sebastienros/jint/issues/3904) and merged by
[#3906](https://github.com/sebastienros/jint/pull/3906). They describe an offer the maintainer can review and
revise. They have **not been delivered** to AngleSharp.Js or Jither, and no adoption agreement is implied.
Source observations below were rechecked against Jint main `a04b5836d590e79ff52c350bf72bc436530daf3f`.

## Proposal for AngleSharp Js

Jint's browser work uses AngleSharp for the parser, DOM and CSSOM. We would like to make the generated
binding work useful to AngleSharp.Js while preserving its choice of scripting services and host lifecycle.
The initial proposal is a small integration experiment: generate `Node`, `Element` and `Document` bindings,
connect them to one AngleSharp document, and prove their behavior and ownership before choosing a package
boundary. The [binding-cost comparison in #3898](https://github.com/sebastienros/jint/issues/3898) is separate;
this proposal claims no measured speedup over reflection bindings.

### What is available to review

| Artifact | What can be reused | Current boundary |
| --- | --- | --- |
| [Metadata reader and emitter](https://github.com/sebastienros/jint/tree/main/tools/dom-bindings/Jint.Browser.BindingGenerator/) | Read AngleSharp attributes with `MetadataLoadContext`; emit static interface calls and checked-in shapes | Generator output currently names `Jint.Browser.Dom` helpers; namespace and runtime targeting need an explicit extraction design |
| [Pin and overrides](https://github.com/sebastienros/jint/blob/main/tools/dom-bindings/README.md) | Versioned input assemblies, WebIDL corrections, diagnostics and skipped-member reports | Some overrides are browser services, such as navigation, parser insertion and custom-element reactions; adopting every override would also adopt those obligations |
| [Generated output](https://github.com/sebastienros/jint/tree/main/Jint.Browser/Dom/Generated/) | Interface shapes, constants, inheritance, conversion call sites and collection accessors | It is source to review, not a standalone consumer library |
| [Binding runtime](https://github.com/sebastienros/jint/blob/main/Jint.Browser/Dom/DomBindings.cs) and [realm](https://github.com/sebastienros/jint/blob/main/Jint.Browser/Dom/DomRealm.cs) | Receiver brands, wrapping, per-engine identity, lazy prototypes and constructors | Both are internal; global installation and principal-realm capture reach `Engine._mainRealm` |
| [Node wrapper](https://github.com/sebastienros/jint/blob/main/Jint.Browser/Dom/DomNodeObject.cs) and [tree dispatcher](https://github.com/sebastienros/jint/blob/main/Jint/WebApi/Events/EventDispatch.cs) | DOM event paths, retargeting and listener dispatch over host-provided tree relationships | `JsEventTarget`, the dispatch entry and tree overrides are internal; this is not a public event adapter today |
| [Host hooks](https://github.com/sebastienros/jint/blob/main/Jint.Browser/Dom/DomHostHooks.cs) | A place for the host to supply lifecycle operations behind generated members | Internal hooks already call browser event/custom-element services; they need separation before another host can implement them |

The engine's [JsObjectShape](https://github.com/sebastienros/jint/blob/main/Jint/Native/JsObjectShape.cs),
[ArrayLikeObject](https://github.com/sebastienros/jint/blob/main/Jint/Native/Object/ArrayLikeObject.cs) and
[NamedPropertyObject](https://github.com/sebastienros/jint/blob/main/Jint/Native/Object/NamedPropertyObject.cs) are public building blocks. That does
not make the complete binding runtime public. Referencing today's `Jint.Browser` package also brings its
page runtime and `Jint.DevTools` dependency; there is no supported binding-only installation entry point.

### Proposed integration experiment

1. Agree with AngleSharp.Js on one target Jint version and the document/engine ownership model. Start with
   one document and one engine. Wrapper identity, prototypes and constructors belong to that engine;
   process-shared shapes must retain no document, engine or script value.
2. Extract a minimal generator fixture containing `Node`, `Element` and `Document`, their inherited
   interfaces, and the collection/conversion helpers required by the selected members. Keep a reviewed
   input pin and explicit report of every omitted member. Retain existing runtime behavior outside this
   fixture until parity is demonstrated.
3. Design the smallest public installation/wrap/unwrap and host-callback boundary the fixture needs.
   Resolve principal-realm access and event dispatch through separately reviewed engine APIs, each proven
   by a consumer without `InternalsVisibleTo`. A blanket friendship grant or making every helper public
   would hide the boundary rather than establish it. No new API signature is promised by this document.
4. Let the adopter own script execution, navigation, parser callbacks and document lifetime. Map each
   selected [host hook](https://github.com/sebastienros/jint/blob/main/Jint.Browser/Dom/DomHostHooks.cs) to that owner's operation. Integrate the tree
   dispatcher only after specifying parent/shadow relationships, listener exceptions, microtask checkpoints
   and activation/default actions; avoid delivering each event through two independent event systems.
5. Compare the fixture against the adopter's existing behavior, then expand interface coverage. Decide
   whether shared source or a separate package is maintainable only after the fixture exposes its true
   dependencies. Carry Jint's [BSD-2-Clause notice](https://github.com/sebastienros/jint/blob/main/LICENSE.txt) with reused source; review packaging
   and upstream license requirements before transferring it.

### Evidence and acceptance conditions

Use the existing suites as executable examples, not as a claim that they validate an extracted package:

- [DomBindingsPinTests](https://github.com/sebastienros/jint/blob/main/Jint.Tests.Browser/DomBindingsPinTests.cs) and
  [DomBindingsStalenessTests](https://github.com/sebastienros/jint/blob/main/Jint.Tests.Browser/DomBindingsStalenessTests.cs): input versions agree;
  regeneration has no unexplained changes or diagnostics.
- [DomCollectionTests](https://github.com/sebastienros/jint/blob/main/Jint.Tests.Browser/DomCollectionTests.cs): collection brands, indexed/named
  reads, prototype descriptors and liveness survive the boundary.
- [DomEventTests](https://github.com/sebastienros/jint/blob/main/Jint.Tests.Browser/DomEventTests.cs): listener ordering and tree behavior use the
  adopter's document, including mutation during dispatch.
- [SharedShapeTests](https://github.com/sebastienros/jint/blob/main/Jint.Tests.PublicInterface/SharedShapeTests.cs): use the public-consumer model
  to prove the extraction needs no friend assembly. Add two-engine identity/isolation and disposal cases
  to the proposed fixture, including a wrapped object first reached from a callback.
- Run the relevant vendored DOM WPT cases in the adopting host; preserve documented
  [AngleSharp divergences](https://github.com/sebastienros/jint/blob/main/Jint.Browser/Dom/divergences.md) rather than implementing a second DOM.
  Consult [the browser lane](https://github.com/sebastienros/jint/blob/main/Jint.Tests.Browser/Wpt/README.md) for the distinction between passing,
  excluded and untriaged results.

The experiment must compile and pass behavior checks through public APIs before calling it adoptable.
Measure cold installation, warmed access and allocations separately using #3898's isolated harness before
making performance claims. Generated interface calls avoid reflection in the member body, but that does
not establish a general AOT contract: [Jint.Browser's project](https://github.com/sebastienros/jint/blob/main/Jint.Browser/Jint.Browser.csproj) explicitly
sets `IsAotCompatible` to false, and a native tool smoke test covers only that closed executable.

The maintainer review should resolve the desired ownership location, minimum target frameworks, selected
interface scope, versioning policy, and who maintains overrides when AngleSharp metadata changes. Delivery
should ask whether this experiment would be useful, rather than present a package split as a settled choice.

## Integration note for Jither

The [Jint.DevToolsProtocol](https://github.com/Jither/Jint.DevToolsProtocol) and
[Jint.DebugAdapter](https://github.com/Jither/Jint.DebugAdapter) projects are useful prior integration work.
The following is a draft note about the engine capabilities now available; it does not assess those
projects' current support status or claim their existing adapters run unchanged.

> Jint's headless-browser campaign also added engine-level debugging APIs for embedders with no browser.
> We have an in-repository CDP implementation and would welcome feedback on whether the new public seams
> address the integration gaps encountered in your tooling. The paths and tests below provide a starting
> point for trying them; DAP translation remains a separate integration task.

| Integration concern | Current Jint path | Evidence to start from |
| --- | --- | --- |
| Pause on exceptions | `DebugHandler.PauseOnExceptions` and the exception pause event; preserve the caught/uncaught distinction | [DebuggerExceptionPauseTests](https://github.com/sebastienros/jint/blob/main/Jint.Tests.DevTools/Session/DebuggerExceptionPauseTests.cs) |
| Discover executable breakpoint positions | `DebugHandler.GetStepLocations(Program)` and its range overload | [StepLocationTests](https://github.com/sebastienros/jint/blob/main/Jint.Tests/Runtime/Debugger/StepLocationTests.cs), which compare enumeration against runtime pauses |
| Evaluate an older stack frame | `DebugHandler.Evaluate(sourceText, CallFrame, ...)` | [DebuggerFrameEvaluationTests](https://github.com/sebastienros/jint/blob/main/Jint.Tests.DevTools/Session/DebuggerFrameEvaluationTests.cs) |
| Associate source and stack positions | `Advanced.TryGetSourceText(Program)` and `CallFrame.Program`; source names alone are not identities | [DebuggerScriptIdentityTests](https://github.com/sebastienros/jint/blob/main/Jint.Tests.DevTools/Session/DebuggerScriptIdentityTests.cs) |
| Accept protocol work without concurrent engine access | `Engine.Tasks.Post(Action)`; a host pumps work on its engine thread, including a defined pause loop | [OffThreadCommandTests](https://github.com/sebastienros/jint/blob/main/Jint.Tests.DevTools/Session/OffThreadCommandTests.cs) and [DebuggerPauseLoopTests](https://github.com/sebastienros/jint/blob/main/Jint.Tests.DevTools/Session/DebuggerPauseLoopTests.cs) |
| Inspect remote values | Public `ValueInspector` (experimental); inspect descriptors without invoking accessors | [ValueInspectorTests](https://github.com/sebastienros/jint/blob/main/Jint.Tests.PublicInterface/ValueInspectorTests.cs) |

For a CDP integration, begin with [Jint.DevTools' engine-only example](https://github.com/sebastienros/jint/blob/main/Jint.DevTools/README.md) and the
[public EngineTarget](https://github.com/sebastienros/jint/blob/main/Jint.DevTools/EngineTarget.cs). `Jint.DevTools` consumes public Jint APIs without a
friend grant. The host still owns engine construction, execution and pumping; protocol socket callbacks
must not execute script directly. Consult the [protocol design](../design/devtools-protocol.md) and
[manifest](https://github.com/sebastienros/jint/blob/main/tools/devtools-protocol/manifest.json) for implemented commands and explicit unsupported
responses. A working CDP example is not evidence that an old adapter's transport or debugger assumptions
are compatible.

For a DAP integration, first choose between translating DAP requests onto CDP or using the public debugger
APIs directly. Neither is an implemented DAP server here. Prototype initialize/attach, breakpoint binding,
stack/scopes/variables, paused-frame evaluation, continue/step and disconnect with real client transcripts.
Account for source/column conversion, variable-reference lifetimes, cancellation, session detach while
paused, and engine replacement. CDP session/domain helpers are not automatically a public DAP abstraction.

The historical per-step allocation concern needs a fresh measurement against the chosen integration.
Debug mode changes interpreter execution paths; the existence of these APIs does not demonstrate an
allocation or throughput improvement. Use normal debugging disabled as a distinct baseline, and report
host/adapter allocations separately from engine stepping before drawing a conclusion.

## Review and delivery record

| Stage | Evidence needed | Status of this document |
| --- | --- | --- |
| Repository preparation | Audited source links, concrete proposals and focused tracking issue | Merged in #3906; #3904 closed |
| Maintainer review | Chosen scope, recipients, revised message and approval to send | Pending |
| External delivery | Links to actual messages or upstream discussions | Not sent |
| Adoption decision | Recipient feedback, agreed experiment and owned follow-up issues | Not requested |

The repository preparation landed in #3906 and #3904 is closed. Campaign X6 stays incomplete until the
maintainer records what was actually offered and the note delivered. An offer being delivered does not mean
it was accepted.
