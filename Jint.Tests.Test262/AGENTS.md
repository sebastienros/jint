# Agent instructions: the test262 conformance suite

> **Read this when:** You are bumping the pinned test262 SHA, triaging a conformance failure, editing `Test262Harness.settings.json`, or bumping the Test262Harness package or its generator tool.
>
> This is one of the co-located instruction files indexed from the repository-root
> [`AGENTS.md`](../AGENTS.md). Read that first — it carries the build and test commands, the branch to
> target, and the conventions that apply to every file in the repository. Nothing below is
> repeated there.

### Where the suite's own sources are

Test sources live in `..\test262\test`, which you may always read, and the harness scripts a test `includes` in `..\test262\harness` — including `harness/sm/` for the staged SpiderMonkey ports. Failure output contains the failing script — strip the line numbers to reproduce.

### Updating the test262 suite

Bump `SuiteGitSha` in `Jint.Tests.Test262/Test262Harness.settings.json` to the new upstream commit. The next build notices the settings-file hash changed, wipes `Generated/` and regenerates it (`dotnet tool restore && dotnet test262 generate`); `Generated/` is gitignored.

The runtime half of that pin is `CorpusContentSha256` in `State.cs`: a canonical digest of every path and
per-file digest under `harness/` and `test/`. Update it with the reviewed corpus whenever `SuiteGitSha` changes.
Leaving the old value fails closed and reports the downloaded tree's actual digest; never bypass that failure
or accept a directory-only archive, because generated source proves which tests exist but not which corpus
content the runner opened.

A bump is a **code** change, not just a pin change. An upstream normative change can turn a feature that passes today red tomorrow, so read the commits in the range rather than only the new test files — `git log --oneline <old>..<new>` in a test262 checkout, plus the `features.txt` diff for newly added features.

A feature that arrives unimplemented parks in `ExcludedFeatures`, and the PR that implements it removes the entry again. Entries in `ExcludedFiles` are **temporary by default**: commented with what removes them, grouped under a banner with a prose reason, and expected to be paid down. Three banners hold that debt — `=== STAGING EXCLUSIONS ===` (SpiderMonkey ports Jint does not yet satisfy), `=== STALE TEST ===` (a file upstream is correcting) and `=== INTL402 EXCLUSIONS ===` together with its Temporal sub-banner (missing CLDR data) — and that invariant is worth keeping.

A fourth banner, `=== PERMANENT EXCLUSIONS ===`, holds what is **not** debt. An entry earns a place there only when the test asserts behaviour *the specification does not require of Jint* — an optional extension some engine ships, or a result the spec deliberately leaves implementation-defined — and its comment records the decision and the normative citation that makes it defensible, rather than a to-do nobody intends to do. Age never promotes an entry: a gap nobody has got round to stays in the temporary list however long it has sat there, because what the banner records is intent, not age. Reversing one is a perfectly normal PR, but it has to argue the decision and rewrite the reasoning — it does not just delete the line. The two groups there today are the legacy `Function.prototype.caller` extension (permitted by §17.1 Forbidden Extensions, required by nothing, and declined because handing script the calling function punches a hole in the isolation Jint's in-process embedders rely on) and `Date.parse`'s non-ISO fallback (§21.4.3.2 makes the fallback format implementation-defined).

`SubDirectories` names which of test262's `test/` sub-directories are generated: `annexB`, `built-ins`, `intl402`, `language` and `staging`. The first four are the harness tool's own default; **`staging` is opted into explicitly** and is not part of a default `Test262Harness.Console` run. It is worth the ~2,800 extra cases: it is largely SpiderMonkey's own suite contributed upstream, and it covers behaviour the stable directories do not reach at all (`parseInt` with a signed hex string, a `Proxy` as the global's prototype). Its helpers live in `harness/sm/` and are included as `sm/non262-Set-shell.js`, which is why `TestHarness.cs` enumerates `harness/` recursively and keys `State.Sources` on the path relative to `harness/` rather than on the file name.

### Updating Test262Harness itself

The harness is two pins. Both ship from one repository under one version number, and they move together, in one PR:

- the **library**, `Test262Harness` in `Directory.Packages.props`. The generated code, `State.cs` and `Test262CorpusAcquisition.cs` compile against it, and it opens the corpus at run time;
- the **generator**, `test262harness.console` in `.config/dotnet-tools.json`, which writes `Generated/`.

Dependabot bumps only the library: #4290 moved it to 1.3.0 and left the generator at 1.2.1. The `CheckTest262HarnessLockstep` target in the project file fails the build while the two versions differ, so a PR like that stays red until the manifest is bumped on it too. The manifest is hashed with the settings file into `Generated/.settings.hash` and into the CI cache key, so a generator bump regenerates the suite both locally and in CI.

Verify a bump by comparing case sets, not files. Since 1.4.0 the generator orders tests ordinally and keeps its template SHA stable, but each file header still names the tool's path and version, and the first regeneration on 1.4.0 reordered every file. What has to match the run before the bump is the sorted set of `[TestCase(...)]` lines and the passed, failed and skipped counts of the full run.

`Test262CorpusAcquisition` uses `Test262StreamExtensions.FromGitHub` only as its download step, into an empty staging directory, so the library's own cache check never runs there. Everything around that step is Jint's: the content digest, offline mode, `JINT_TEST262_CACHE`, first-publisher-wins publication and the bounded retry of transient failures. Since 1.4.0 the library downloads atomically and downloads a damaged archive again, but it publishes an archive before anything has checked what is in it, and it replaces an archive that another process has already published. So do not swap the wrapper for a direct `FromGitHub(sha, cacheDirectory)` call.
