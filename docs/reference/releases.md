# Branches and Releases

| Branch | Purpose |
| --- | --- |
| `main` | Development of Jint 5, including breaking changes |
| `4.x` | Compatible correctness and conformance fixes for Jint 4.16 |
| `3.x` | Security and severe-correctness maintenance for the Esprima-based line |

Pull requests target `main` unless they are explicit backports.

Development packages are published to
[Feedz.io](https://feedz.io/org/sebastienros/repository/jint/packages). Add
`https://f.feedz.io/sebastienros/jint/nuget/index.json` as a NuGet source to consume them. NuGet releases are
created from version tags on the branch being released.

See [Migrating to Jint 5](../guide/migrating-to-v5.md) when moving from the `4.x` line.
