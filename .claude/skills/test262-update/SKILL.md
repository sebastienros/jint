---
name: test262-update
description: Bump Jint's pinned tc39/test262 SHA in Jint.Tests.Test262/Test262Harness.settings.json, triage the upstream commit range for normative changes, run the conformance suite, and fix whatever the new tests expose. Use when asked to update/upgrade test262, bump SuiteGitSha, pull in the latest conformance tests, or investigate a test262 conformance failure after a bump.
---

# Updating the pinned test262 suite

The visible change is one line — `SuiteGitSha` in `Jint.Tests.Test262/Test262Harness.settings.json`.
Treat it as a **code** change anyway. Upstream lands tests for normative changes that Jint has not
implemented, and those turn green features red. The whole job is triaging the range before you run
anything, then fixing the engine — never the tests.

## Ground rules

- **Never edit or "fix" a test262 test.** If a test is wrong, the answer is an engine fix or, as a last
  resort, an `ExcludedFiles` entry.
- **test262 at the pinned SHA outranks the published prose.** See [Normative changes](#3-normative-changes-the-part-that-actually-costs-time).
- Exclusions outside `intl402/` must be temporary and carry a comment naming what removes them. Today every
  `ExcludedFiles` entry is intl402 — keep that invariant.
- A feature that arrives unimplemented parks in `ExcludedFeatures`; the PR that implements it removes the entry.
- Work in a worktree, not the primary checkout, and start from a fresh `main`.

## 0. Orient

| Thing | Where |
| --- | --- |
| The pin | `Jint.Tests.Test262/Test262Harness.settings.json` → `SuiteGitSha` |
| Local suite checkout (read-only aid) | `../test262` — `origin` = your fork, `upstream` = `tc39/test262` |
| Generator | `dotnet test262 generate`, from `Jint.Tests.Test262/.config/dotnet-tools.json` |
| Generated tests | `Jint.Tests.Test262/Generated/` — **gitignored**, so the committed diff stays one line |

The runner does **not** read `../test262`. `State.Test262StreamLoader` downloads the suite from GitHub at
`SuiteGitSha`, so the local checkout exists only so you can read the range. Fetch it anyway — you cannot
triage without it.

The `GenerateTestSuite` MSBuild target hashes the settings file into `Generated/.settings.hash`. Change any
byte of the settings and the next build wipes `Generated/` and regenerates. So: never pass `--no-build`, and
if you suspect a stale generation, check the hash file rather than guessing.

## 1. Establish the range

```bash
cd ../test262 && git fetch upstream
git log --oneline <pinned-sha>..upstream/main            # or ..<target-sha> if one was given
git diff <pinned-sha>..upstream/main -- features.txt
git diff --name-only <pinned-sha>..upstream/main | grep -v '^test/'
```

**Order commits by ancestry, never by author date.** test262 merges rebased branches, so a commit *authored*
earlier routinely lands on top of one authored later — a real bump had `3655e746` (authored 08-03) sitting
above `be13516f` (authored 08-07). Use `--pretty='%h %cd %s'` or `git merge-base --is-ancestor` when the
ordering matters, and confirm the true remote tip with `git ls-remote https://github.com/tc39/test262.git main`.

## 2. Triage every commit

Read the commits, not just the changed-file list.

| Signal | Meaning |
| --- | --- |
| only `.github/**`, `tools/**`, `package.json` | infra — zero impact on the run |
| `features.txt` gained a line | a new feature; if Jint lacks it, it parks in `ExcludedFeatures` with a comment |
| `src/**.case` / `src/**.template` | procedurally generated tests changed; read the generated output, not the template |
| new files under `test/built-ins/**` or `test/language/**` | **the risky class** — go to step 3 |
| a commit message naming a proposal, "normative", or an `ecma262#NNNN` | **stop and read the algorithm diff** |

For a small range, just read the new tests — `git show <sha>:test/path/to/new-test.js`. A test's `info:` block
usually quotes the numbered algorithm verbatim, which is the fastest way to see what changed and the most
reliable way to get argument-validation order right.

For each new behavioural test, check the engine **before** running the suite. `Jint.Repl` turns a 3-minute
suite run into a 10-second answer:

```bash
dotnet run --project Jint.Repl -c Release -- -f scratch.js -t 10   # always pass -t
```

Write the assertions as plain `if (…) throw new Error('FAIL …')` — the REPL has no test262 harness. Strip the
`$DONOTEVALUATE()` / `includes:` lines; for `flags: [async]` tests, assert on `.then(...)` callbacks.

## 3. Normative changes — the part that actually costs time

**The published spec can be behind test262.** A normative PR that reaches consensus gets test262 coverage
while `tc39.es/ecma262` still renders the old algorithm, and an already-merged proposal's own page
(`tc39.es/proposal-*/`) is frozen at its pre-merge text and will confidently show you the wrong steps. Both
happened on the `Promise.try` bump: the proposal page and the living spec agreed with each other and with
Jint, and all three were what the new tests were written to reject.

Find the real change through the repo, not the rendered spec:

```bash
gh api -X GET search/issues -f q='repo:tc39/ecma262 <feature> <keyword>' \
  --jq '.items[] | "\(.number) \(.state) \(.title)"'
gh pr view <n> --repo tc39/ecma262 --json title,state,mergedAt,labels
gh pr diff <n> --repo tc39/ecma262        # the spec.html algorithm diff is the authority
```

Labels tell you the status: `has consensus` + `needs implementations` means implement it. `mergedAt: null` is
**not** a reason to skip — AGENTS.md is explicit that where prose and test262 disagree, test262 at the pinned
SHA wins.

When you implement one:

- Cite the document that owns the feature now. A merged proposal moves to `https://tc39.es/ecma262/#sec-...`;
  the anchors get renamed on the way in.
- Add a `<remarks>` naming the normative PR, so the next reader knows why the code and the current published
  prose disagree.
- **Re-check the paths the reorder newly reaches.** Moving a step can expose a latent bug that the old
  ordering made unreachable — see the gotchas below, where hoisting a call above a capability construction
  turned a dormant `Promise.try()` crash into a live one.

## 4. Bump, build, run

```bash
# edit SuiteGitSha, then:
dotnet test -c Release Jint.Tests.Test262/Jint.Tests.Test262.csproj
```

The build regenerates before it compiles; no separate generate step. ~3 minutes, ~99.9k tests. No runner
timeout needed — the engine defaults to 30 s per test.

Verify the regeneration actually happened rather than trusting a green run over a stale `Generated/`:

```bash
grep -c "<new-test-name>" Jint.Tests.Test262/Generated/Tests262Harness.Tests.language.generated.cs
```

A negative parse test appears **twice** per file (strict + sloppy). Deleted upstream tests should be gone.

Then, because a spec fix moves engine behaviour that unit tests pin:

```bash
dotnet test -c Release Jint.Tests/Jint.Tests.csproj
dotnet test -c Release Jint.Tests.PublicInterface/Jint.Tests.PublicInterface.csproj
```

## 5. Fixing what fails

Failure output contains the failing script — strip the line numbers to reproduce it in `Jint.Repl`. Put a
regression test in `Jint.Tests` for anything test262 does **not** cover (test262 tests the spec, not Jint's
internals; a crash on an input no test262 test exercises still needs a pin).

## 6. Decide whether there is a PR at all

If the suite is green with only the pin moved, there is nothing to ship but a one-line pin bump — say so and
ask before opening a PR. A bump that needs engine fixes is a real PR: pin + fix + regression tests, targeting
`sebastienros/jint` `main`.

```bash
gh pr create --repo sebastienros/jint --head <you>:<branch> --base main
```

Keeping the local suite fork current is a separate, optional courtesy:

```bash
cd ../test262 && git fetch upstream && git merge --ff-only upstream/main   # then push origin main if wanted
```

## Gotchas this task has actually hit

- **Author date lies about ordering.** `%ad` is not topology; use `%cd` or `--is-ancestor`.
- **The proposal page is frozen at merge.** `tc39.es/proposal-promise-try/` still shows the pre-normative
  algorithm. Never cite it as current for a merged feature.
- **`spec.html` is too big for a fetch tool.** Asking a fetcher for one section of the living spec returns a
  truncated prefix and a confident "not found". Go through `gh pr diff` instead.
- **A reorder exposes latent bugs.** `Promise.try` moved `NewPromiseCapability` after the callback call; the
  argument-slicing that the old ordering never reached with zero arguments then crashed.
- **`arguments.AsSpan()` is a trap in a built-in.** `ExpressionCache.ArgumentListEvaluation` hands the callee
  `Unsafe.As<JsValue[]>(object?[])` whenever the argument list is fully cached — *including the zero-argument
  case* — so `AsSpan()` throws `ArrayTypeMismatchException` ("Attempted to access an element as a type
  incompatible with the array"). Use `arguments.Skip(n)`, which copies element-wise and returns `[]` when
  there is nothing to take.
- **`JsValue.Call` on a non-callable throws a CLR `ArgumentException`, not a JS `TypeError`.** Where the spec
  says `Call(F, V, args)`, write `GetCallable(f).Call(...)` so the failure is a `JavaScriptException` your
  `Completion` handling can turn into a rejection.
- **`Promise`-shaped fixes need the abrupt path checked too.** `Completion(...)` wrapping means a throw
  inside the step becomes a rejection, while a throw from a step *outside* it propagates to the caller. Tests
  distinguish the two (`ctx-non-ctor.js` expects a synchronous `TypeError`; `throws.js` expects a rejection).
