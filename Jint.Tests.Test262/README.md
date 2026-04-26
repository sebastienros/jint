To generate test suite, run:

```
dotnet tool restore
dotnet test262 generate
```

## Reusable provider examples

`NodaTimeZoneProvider.cs` and `IcuCldrProvider.cs` in this directory are not just test
fixtures — they are working examples of the provider extension points described in the
main [README](../README.md#extending-temporal-and-intl-with-custom-providers). The test
suite reaches its current test262 conformance numbers by registering them on the
`Engine.Options.Temporal.TimeZoneProvider` and `Engine.Options.Intl.CldrProvider`
properties; end users can copy these files into their own projects (with the matching
`NodaTime` / `ICU4N` NuGet packages) to get the same behaviour.
