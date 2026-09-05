# ECMAScript Support

Jint implements modern ECMAScript directly from the specification and validates behavior against
[test262](https://github.com/tc39/test262).

## Editions

The engine supports language features through ECMAScript 2025, including modules, async functions, generators,
typed arrays, `BigInt`, private fields, top-level `await`, iterator helpers, explicit resource management, and
`Temporal`.

Selected proposals are also available before they receive an edition number. Proposal APIs can change as their
specifications evolve.

Current proposal-stage features include decorators, explicit resource management, immutable `ArrayBuffer`,
import bytes, iterator chunking and sequencing, JSON parse source text access, `Math.sumPrecise`, `ShadowRealm`,
`Temporal`, `Uint8Array` base64 conversion, and upsert operations. Jint follows each proposal's current
specification rather than treating proposal behavior as a frozen contract.

## Runtime differences

ECMAScript does not define browser or Node.js globals. Jint therefore installs neither by default:

- enable standards-based runtime APIs through [Web APIs](../guide/web-apis.md);
- enable supported Node modules through [Node compatibility](../guide/node-compatibility.md);
- use [`Jint.Browser`](../packages/jint-browser/) when scripts require a document and DOM.

## Measured conformance

At the pinned test262 commit, Jint passes **102,585 of 102,692 generated cases (99.9%)**. The run covers
`annexB`, `built-ins`, `intl402`, `language`, and `staging`; 107 generated cases are skipped.

This percentage describes the configured corpus, not every file in test262. The active pin and the reasons for
each exclusion are recorded in
[`Test262Harness.settings.json`](https://github.com/sebastienros/jint/blob/main/Jint.Tests.Test262/Test262Harness.settings.json).
