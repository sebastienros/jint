<!-- Thanks for contributing to Jint! Please fill in the sections below. -->

## Summary

<!-- One short sentence (under 80 characters) describing the change. -->

## Details

<!--
- What changed and why
- Anything reviewers should pay extra attention to
- Notable implementation choices or trade-offs
-->

## Linked issue

<!-- Use "Fixes #1234" or "Closes #1234" so the issue is auto-closed on merge. For discussion-only changes use "Refs #1234". -->

Fixes #

## Test plan

- [ ] Added or updated unit tests in `Jint.Tests`
- [ ] Ran `dotnet test --configuration Release` locally
- [ ] For ECMAScript spec changes: ran `Jint.Tests.Test262` and confirmed no regressions
- [ ] For interop changes: covered in `Jint.Tests/Runtime/Interop`
- [ ] For perf changes: included before/after numbers from `Jint.Benchmark`

## Breaking change?

<!-- Yes / No. If yes, describe the public-API impact and any migration steps. -->
