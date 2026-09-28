# The DOM binding contract

`contract.json` is the checked-in source for `Jint.Browser/Dom/Generated/*.g.cs`. It explicitly records every interface, parent, wrapper kind, member body, descriptor, method length, constant, collection accessor and string enum. The emitter reads this file without loading or reflecting over AngleSharp assemblies. A change to a browser binding is a change to this contract, followed by regeneration and review of the generated C# diff. Never hand-edit a `.g.cs` file.

Regenerate and verify with:

```bash
JINT_DOM_BINDINGS=update dotnet test -c Release --project Jint.Tests.Browser/Jint.Tests.Browser.csproj --filter FullyQualifiedName~DomBindingsStalenessTests
dotnet test -c Release --project Jint.Tests.Browser/Jint.Tests.Browser.csproj --filter FullyQualifiedName~DomBindingsStalenessTests
```

The command-line emitter also accepts `--contract tools/dom-bindings/contract.json --output Jint.Browser/Dom/Generated`. `DomBindingsStalenessTests` compares its output byte for byte with the checked-in files and checks that the contract reports no diagnostics. `overrides.json` and `pin.json` retain historical decisions and assembly provenance; they neither load assemblies nor constrain benchmark package versions. The one-time upstream assembly extraction switches have been retired. Generation uses `contract.json` alone, with native `Jint.HtmlParser` adapters keeping each JavaScript-visible interface and member reviewable.

`BindingGenerator.Run` loads `BindingContract` and passes its model directly to `Emitter`.
The loader rejects unsupported contract versions, duplicate interface/field/member names and a child
listed before its parent. Extraction-era `ModelBuilder`, `Inventory`, `Conversions` and `Nullability`
sources are not on that path: their attribute-reading rules are not the current generation contract.
Editing `overrides.json` alone therefore changes no emitted binding.

AngleSharp packages are retained only in `Jint.Benchmark` for comparisons with `Jint.HtmlParser`.
`ParserDependencyTests` enforces this boundary. Binding and parser regression tests use the explicit
contract and expected native tree structures rather than loading an upstream implementation.
