# Reflection-free managed trimming

This example publishes an unrooted, self-contained Jint consumer with full managed
trimming and all linker diagnostics treated as errors. It does not add warning
suppressions or preserve entire assemblies.

```sh
dotnet publish Jint.TrimmedExample/Jint.TrimmedExample.csproj -c Release -r linux-arm64 -o /tmp/jint-trimmed
/tmp/jint-trimmed/Jint.TrimmedExample
```

Use the RID for the machine running the executable. NativeAOT is a separate
qualification; this example does not enable or claim it.

The application sets `Jint.Runtime.Interop.ReflectionEnabled=false` as a trimmed
runtime configuration option. The switch defaults to true, preserving existing
CLR interop behavior. It is a process-wide publication choice, not a per-engine
setting.

With reflection disabled, use native `JsValue` values, `ClrFunction` callbacks,
and explicit `ObjectConverter` adapters. Null, booleans, numeric primitives,
characters and strings still convert through `JsValue.FromObject`. Arbitrary
CLR objects, reflected extension methods and CLR operator overloading are
unavailable. Broad CLR interop options are rejected. Generated CLR wrappers are
not qualified by this example.

The Promise probe posts settlement from a worker to the engine queue. The worker
does not enter JavaScript; the owning thread pumps and unwraps the result.

The same program checks the unchanged default CLR projection in an untrimmed
process that omits the feature switch:

```sh
dotnet run --project Jint.TrimmedExample/Jint.TrimmedExample.csproj -c Release -p:PublishTrimmed=false -p:JintReflectionEnabled=default
```
