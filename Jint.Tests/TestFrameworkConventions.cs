#nullable enable

// The two decisions that make an NUnit run of these suites mean what the xUnit run meant. Both are stated
// once, for the whole assembly, rather than repeated on 500 fixtures — and this file is compiled into
// Jint.Tests.PublicInterface as well, so the two suites cannot drift apart on either of them.

// xUnit constructs a new instance of a test class for every test; NUnit's default is one instance per
// fixture, reused. Nothing in either suite was written against the reused instance — EngineTests,
// InteropTests, MethodAmbiguityTests, SamplesTests, UuidTests and Jint.Tests.PublicInterface's InteropTests
// all build an Engine in their constructor and expect it fresh — so the reused instance would silently share
// an engine, and its realm, its intrinsics and whatever a previous test wrote to the global object, across
// every test of the class. InstancePerTestCase restores xUnit's contract exactly: the constructor runs per
// test case, and a fixture implementing IDisposable is disposed per test case (explicitly implemented
// Dispose included).
[assembly: FixtureLifeCycle(LifeCycle.InstancePerTestCase)]

// xUnit's default is one collection per test class, collections running in parallel with each other and the
// tests inside one running sequentially. ParallelScope.Fixtures is that same granularity: fixtures in
// parallel, the tests of a fixture one at a time. It is deliberately not ParallelScope.All — several
// fixtures hold state across their own tests (SharedObjectShapeTests' shared shapes, the engines built in a
// constructor) and xUnit never ran those concurrently.
//
// The classes that must not run beside anything at all carry [NonParallelizable], which is NUnit's spelling
// of xUnit's [CollectionDefinition(DisableParallelization = true)]: NUnit runs work in shifts and the
// non-parallel shift has a single worker, so a [NonParallelizable] fixture runs with no parallel fixture in
// flight *and* with no other [NonParallelizable] fixture in flight. That second property is what four
// classes sharing one xUnit collection relied on, and it is what the garbage-collection fixtures need.
[assembly: Parallelizable(ParallelScope.Fixtures)]
