To generate test suite, run:

```
dotnet tool restore
dotnet test262 generate
```

The test run downloads the archive for the generated suite's exact `SuiteGitSha` into a unique
staging directory, verifies the pinned corpus content digest, and atomically publishes it to the
cache. Transient acquisition failures are retried up to three times. Set `JINT_TEST262_CACHE` to
choose the directory that holds `test262-<sha>.zip`. Set `JINT_TEST262_OFFLINE=1` to require that
exact cached archive and disable network acquisition; a missing, partial, corrupt, or mismatched
archive fails the run rather than falling back to another corpus.

## Reusable provider examples

`NodaTimeZoneProvider.cs` and `IcuCldrProvider.cs` in this directory are not just test
fixtures — they are working examples of the provider extension points described in the
main [README](../README.md#extending-temporal-and-intl-with-custom-providers). The test
suite reaches its current test262 conformance numbers by registering them on the
`Engine.Options.Temporal.TimeZoneProvider` and `Engine.Options.Intl.CldrProvider`
properties; end users can copy these files into their own projects (with the matching
`NodaTime` / `ICU4N` NuGet packages) to get the same behaviour.
