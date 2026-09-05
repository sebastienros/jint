# Native AOT and Trimming

## Jint

The core package is marked AOT-compatible on modern target frameworks and is exercised through a published native
application in CI.

Reflection-based CLR interop still depends on the host preserving the types and members that scripts reach.
Generic instantiations over value types discovered only at runtime are a known limitation. Prefer explicit host
types and generated accessors for a predictable native build.

## Jint.DevTools

`Jint.DevTools` is Native AOT compatible. Protocol JSON uses source-generated serialization.

## Browser packages

`Jint.Browser` and packages built on it are not currently trim- or AOT-compatible because AngleSharp discovers
parts of its model through reflection without trimming annotations.

See the [Jint 5 migration guide](../v5-migration.md) for the complete compatibility contract.
