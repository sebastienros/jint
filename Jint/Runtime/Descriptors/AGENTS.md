# Agent instructions: property descriptors and lazy values

> **Read this when:** You are adding or changing a `PropertyDescriptor`, a `PropertyFlag`, or any hook that makes a property's value lazy.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../../../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there. The objects these descriptors hang on, and the two lazy mechanisms that are *not* a
> descriptor, are in [`Jint/Native/Object/AGENTS.md`](../../Native/Object/AGENTS.md).

### What counts as a public contract

These are this area's rows of Jint's public surface. The rule they all obey — **a change to any of it
is a row in [`docs/v5-migration.md`](../../../docs/v5-migration.md), written in the same pull request**, even
when it breaks nothing at compile time — and the engine-wide rows are in
[`Jint/AGENTS.md`](../../AGENTS.md#what-counts-as-a-public-contract).

| Surface | Location |
| --- | --- |
| `PropertyDescriptor` and `PropertyFlag.CustomJsValue`, plus `PropertyDescriptor.CreateLazy` — the sanctioned factory for a value that is lazy once | `Jint/Runtime/Descriptors/` |

### Lazy values

**`PropertyFlag.CustomJsValue` is the supported lazy-value hook.** A `PropertyDescriptor` subclass overriding `CustomValue` keeps working under the read inline caches: every caching lane returns through `ObjectInstance.UnwrapJsValue`, which re-reads the flag on each hit and caches the descriptor *reference*, never a value snapshot. Overriding `Get` is now also honoured — a subclass that overrides it is derived `Exotic` (see [the subclassing cliff](../../Native/Object/AGENTS.md#the-subclassing-cliff)) and every read routes through it — but that correctness costs it the descriptor lanes entirely. Prefer `CustomJsValue` when the value is lazy but the property is otherwise ordinary. (The *write* fast path and the global-identifier cache deliberately decline to cache `CustomJsValue` descriptors; that is correct-but-uncached, not broken.) One thing such a descriptor is outside the reach of: `RestoreGlobalSnapshot` reverts a descriptor by writing the inherited `_flags`/`_value` fields, so a `CustomValue` override that keeps its value anywhere else is reinstated by reference and has its attributes reverted, but not its value — writing the inherited field would only desynchronize it from the value reads resolve to. The in-box lazies opt in by implementing `IFieldBackedLazyDescriptor`, which asserts exactly that those two fields *are* their state; nothing outside the assembly can claim it.

**For a value that is lazy *once*, `PropertyDescriptor.CreateLazy` beats a hand-written `CustomValue` override.** The flag stays the hook for a value that stays computed — projected live out of host state, different on every read. But a host descriptor that resolves once and then holds a constant used to keep the flag for the rest of its life, so the write fast path and the global-identifier cache went on declining it long after the laziness was gone — and there was no sanctioned way to build such a descriptor by hand at all, since the `PropertyDescriptor(JsValue?, PropertyFlag)` doc warns hosts off passing `CustomJsValue` themselves. The factory returns an *opaque* descriptor (a `LazyPropertyDescriptor<TState>` — the type is not derivable, which is what keeps the `IFieldBackedLazyDescriptor` claim and the cache-rejoining contract inside the assembly), memoizes into `_value`, then drops the flag and rejoins both lanes. Materialization means a value exists, by whichever route: a **write** counts, and the in-box lazies got the same fix, because a write means the factory can never run afterwards. Two routes clear it — the `CustomValue` setter, and the getter finding a value already there, which is how a raw `_value` store (`ObjectInstance.Set`'s dictionary fast path writes the field directly) gets picked up on the next read.
