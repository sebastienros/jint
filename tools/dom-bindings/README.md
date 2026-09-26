# The DOM binding contract

`contract.json` is the checked-in source for `Jint.Browser/Dom/Generated/*.g.cs`. It explicitly records every interface, parent, wrapper kind, member body, descriptor, method length, constant, collection accessor and string enum. The emitter reads this file without loading or reflecting over AngleSharp assemblies. A change to a browser binding is a change to this contract, followed by regeneration and review of the generated C# diff. Never hand-edit a `.g.cs` file.

Regenerate and verify with:

```bash
JINT_DOM_BINDINGS=update dotnet test -c Release --project Jint.Tests.Browser/Jint.Tests.Browser.csproj --filter FullyQualifiedName~DomBindingsStalenessTests
dotnet test -c Release --project Jint.Tests.Browser/Jint.Tests.Browser.csproj --filter FullyQualifiedName~DomBindingsStalenessTests
```

The command-line emitter also accepts `--contract tools/dom-bindings/contract.json --output Jint.Browser/Dom/Generated`. `DomBindingsStalenessTests` compares its output byte for byte with the checked-in files and checks that the contract reports no diagnostics. `overrides.json` and `pin.json` retain the decisions and original assembly provenance used to extract the initial contract. The one-time `--extract-core`, `--extract-css` and `--overrides` CLI switches recapture that old surface for comparison; routine generation uses `contract.json` alone. The browser migration replaces the old CLR receiver types and calls in this contract with native `Jint.HtmlParser` adapters, keeping each JavaScript-visible interface and member reviewable.
