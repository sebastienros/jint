# Agent instructions: writing against the modern BCL

> **Read this when:** You are writing a call site anywhere in `Jint/` that needs an API one of the older target frameworks (`net472`, `netstandard2.0`, `netstandard2.1`) does not have. This file governs the whole assembly, not just this directory.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there.

### Write against the modern BCL, and polyfill downwards

`Jint` multi-targets `net472;netstandard2.0;netstandard2.1;net8.0;net10.0`, so the oldest target decides what compiles. **Write the call site the way you would on .NET 10 anyway, and add the missing API to `Jint/Extensions/Polyfills.cs`** rather than open-coding the old-framework equivalent everywhere or scattering `#if` through the logic.

Use a C# extension member so the call reads identically on every target framework, and guard it with the narrowest symbol for the frameworks that actually lack the API:

```csharp
internal static class DoublePolyfills
{
    extension(double)
    {
#if NETFRAMEWORK || NETSTANDARD2_0
        // double.IsFinite arrived in .NET Core 3.0 / netstandard2.1.
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
#endif
    }
}
```

Two things about that shape are load-bearing. The container is **`internal`**: it exists only to host polyfills, and a `public` one both leaks a type whose members differ per target framework and invites `CS0104` in any embedder that has a class of the same name and a `using Jint;`. And there is **one container per receiver type** — a *static* extension member lowers into its containing class with the receiver type erased from the signature, so `int.Parse(ReadOnlySpan<char>, IFormatProvider?)` and `long.Parse(ReadOnlySpan<char>, IFormatProvider?)` differ only by return type and collide with `CS0111` if they share a class. Hence `Int32Polyfills`, `Int64Polyfills`, `DoublePolyfills` are separate; instance extension members have no such constraint and all live on `Polyfills`.

**Mirror the BCL signature exactly, defaults included.** An optional parameter the real API does not have changes overload resolution on the frameworks that *do* have it, so the polyfill quietly stops being one — `int.Parse(ReadOnlySpan<char>, IFormatProvider?)` takes no default on `provider` precisely because the BCL's doesn't.

**A polyfill must match the downlevel API's *behaviour*, not just its signature.** `Polyfills.Order` is a hand-written merge sort rather than a call to `OrderBy` because LINQ's sort on .NET Framework is a quicksort with no depth limit and no fallback, so an inconsistent comparer — which a JavaScript comparison function is allowed to be — makes it spin forever instead of terminating. Sorting and searching are where downlevel implementations differ most; check them rather than assuming the older framework's version is merely slower.

A real member always beats an extension member, so on the frameworks that *do* have the API the BCL implementation is what binds — usually the better one (`double.IsFinite` is a single exponent-bits test where the polyfill is two calls). There is no ambiguity to resolve, and nothing to undo when a target framework is eventually dropped: delete the guarded block and every call site is already correct.

This matters beyond tidiness. Spec algorithms are phrased in terms like "if *x* is finite", so a call site reading `double.IsFinite(x)` can be checked against the spec text at a glance where `!double.IsNaN(x) && !double.IsInfinity(x)` has to be decoded first. `Polyfills.cs` is the single home for these — `string.Join(char, …)` and the `ReadOnlySpan<char>` `Parse`/`TryParse` overloads are already there. Add to it rather than working around it.
