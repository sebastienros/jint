# Historical HTML parser replacement inventory (A1)

`inventory.lock.json` preserves the reviewed AngleSharp migration inventory referenced by the parser
architecture designs. It records the source and binding surface at that stage of the replacement;
it is historical evidence, rather than a current dependency, API or conformance baseline.

The migration scanner and its tests have been retired. Current checks live beside the contracts they
verify: `Jint.Tests.Browser/ParserDependencyTests.cs` polices the dependency boundary,
`DomBindingsStalenessTests` verifies generated bindings, public API snapshot tests approve shipped
surfaces, and the WPT suites own their corpus and exclusions. The unsigned fresh-feed consumer under
`tools/html-parser-package-consumer` checks packaged parser APIs and Browser integration.

Do not refresh this historical lock when changing current code. Run the relevant contract tests instead.
